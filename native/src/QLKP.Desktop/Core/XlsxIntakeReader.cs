using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace QLKP.Desktop.Core {
 // Local, first-worksheet .xlsx text extractor; never executes workbook formulas or macros.
 // Explicit limits prevent decompression bombs and accidental giant imports.
 public static class XlsxIntakeReader {
  static readonly XNamespace Main="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
  static readonly XNamespace DocRel="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
  static readonly XNamespace PackageRel="http://schemas.openxmlformats.org/package/2006/relationships";
  static XDocument SafeXml(ZipArchiveEntry entry,long maxBytes) {
   if(entry==null)throw new InvalidDataException("Tệp Excel thiếu thành phần bắt buộc.");
   if(entry.Length>maxBytes)throw new InvalidDataException("Thành phần Excel vượt giới hạn cho phép.");
   using(var stream=entry.Open())using(var reader=XmlReader.Create(stream,new XmlReaderSettings{
    DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=maxBytes,IgnoreComments=true}))
    return XDocument.Load(reader);
  }
  static string CellText(XElement cell,List<string> shared) {
   if(cell.Element(Main+"f")!=null)throw new InvalidDataException("Tệp chứa công thức; hãy chuyển vùng nhập sang giá trị tĩnh trước khi nạp.");
   string kind=(string)cell.Attribute("t")??"";
   if(kind=="inlineStr")return String.Concat(cell.Descendants(Main+"t").Select(x=>x.Value));
   string value=(string)cell.Element(Main+"v")??"";
   if(kind=="s"){
    int index;
    if(!Int32.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out index)||index<0||index>=shared.Count)
     throw new InvalidDataException("Chỉ số chuỗi Excel không hợp lệ.");
    return shared[index];
   }
   if(kind=="e")throw new InvalidDataException("Tệp Excel có ô lỗi; hãy sửa trước khi nhập.");
   return value;
  }
  static int ColumnIndex(string reference) {
   int value=0,idx=0;
   while(idx<reference.Length && Char.IsLetter(reference[idx])){
    char ch=Char.ToUpperInvariant(reference[idx]);
    if(ch<'A'||ch>'Z')throw new InvalidDataException("Tọa độ ô Excel không hợp lệ.");
    value=checked(value*26+(ch-'A'+1));idx++;
    if(value>10000)throw new InvalidDataException("Vùng cột Excel quá lớn.");
   }
   if(value<1)throw new InvalidDataException("Thiếu tên cột ô Excel.");
   return value-1;
  }
  static string SafeSheetPath(XDocument workbook,XDocument rels) {
   var first=workbook.Descendants(Main+"sheet").FirstOrDefault();
   if(first==null)throw new InvalidDataException("Sổ Excel không có trang tính.");
   string relationshipId=(string)first.Attribute(DocRel+"id");
   var rel=rels.Descendants(PackageRel+"Relationship").FirstOrDefault(x=>(string)x.Attribute("Id")==relationshipId);
   if(rel==null)throw new InvalidDataException("Excel không có liên kết tới trang tính đầu tiên.");
   if(String.Equals((string)rel.Attribute("TargetMode"),"External",StringComparison.OrdinalIgnoreCase))
    throw new InvalidDataException("Không đọc trang tính liên kết ngoài.");
   string target=((string)rel.Attribute("Target")??"").Replace('\\','/');
   if(target.StartsWith("/xl/",StringComparison.OrdinalIgnoreCase))target=target.TrimStart('/');
   else if(target.StartsWith("worksheets/",StringComparison.OrdinalIgnoreCase))target="xl/"+target;
   else throw new InvalidDataException("Đường dẫn trang tính Excel không được hỗ trợ.");
   if(target.Contains("..")||!target.StartsWith("xl/worksheets/",StringComparison.OrdinalIgnoreCase)||!target.EndsWith(".xml",StringComparison.OrdinalIgnoreCase))
    throw new InvalidDataException("Đường dẫn trang tính bất thường.");
   return target;
  }
  static string EscapeTsv(string s) {
   if(s.IndexOfAny(new[]{'\t','\r','\n','"'})>=0)return "\""+s.Replace("\"","\"\"")+"\"";
   return s;
  }
  public static string ReadFirstSheet(string path) {
   if(String.IsNullOrWhiteSpace(path) || !File.Exists(path))throw new FileNotFoundException("Không tìm thấy tệp Excel.");
   if(new FileInfo(path).Length>12*1024*1024)throw new InvalidDataException("XLSX vượt 12 MiB; tách bảng nguồn trước khi nhập.");
   using(var archive=ZipFile.OpenRead(path)) {
    if(archive.Entries.Count>2000)throw new InvalidDataException("Tệp Excel có quá nhiều thành phần.");
    var workbook=SafeXml(archive.GetEntry("xl/workbook.xml"),2*1024*1024);
    var rels=SafeXml(archive.GetEntry("xl/_rels/workbook.xml.rels"),2*1024*1024);
    var sheet=SafeXml(archive.GetEntry(SafeSheetPath(workbook,rels)),7*1024*1024);
    var shared=new List<string>();
    var sharedEntry=archive.GetEntry("xl/sharedStrings.xml");
    if(sharedEntry!=null){
     var sharedXml=SafeXml(sharedEntry,5*1024*1024);
     foreach(var item in sharedXml.Descendants(Main+"si")){
      shared.Add(String.Concat(item.Descendants(Main+"t").Select(x=>x.Value)));
      if(shared.Count>100000)throw new InvalidDataException("Tệp Excel có quá nhiều ô văn bản.");
     }
    }
    var lines=new List<string>();
    int lineNumber=0, headerWidth=0;
    foreach(var row in sheet.Descendants(Main+"sheetData").Elements(Main+"row")){
     if(++lineNumber>5001)throw new InvalidDataException("Chỉ hỗ trợ tối đa 5.000 dòng dữ liệu cộng tiêu đề.");
     var values=new string[20];int largest=-1;
     foreach(var cell in row.Elements(Main+"c")){
      int index=ColumnIndex((string)cell.Attribute("r")??"");
      if(index>=20)throw new InvalidDataException("Chỉ hỗ trợ tối đa 20 cột dữ liệu.");
      values[index]=CellText(cell,shared);
      if(values[index].Length>300)throw new InvalidDataException("Ô Excel dài trên 300 ký tự.");
      largest=Math.Max(largest,index);
     }
     if(largest<0)continue;
     if(headerWidth==0)headerWidth=largest+1;
     if(largest>=headerWidth)throw new InvalidDataException("Dòng Excel có cột vượt ngoài tiêu đề.");
     lines.Add(String.Join("\t",values.Take(headerWidth).Select(x=>EscapeTsv(x??""))));
    }
    string raw=String.Join("\n",lines);
    if(raw.Length>2*1024*1024)throw new InvalidDataException("Bảng Excel quá lớn sau giải nén.");
    return raw;
   }
  }
 }
}
