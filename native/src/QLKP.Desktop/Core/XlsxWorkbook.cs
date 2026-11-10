using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace QLKP.Desktop.Core {
 internal static class XlsxWorkbook {
  static readonly XNamespace Main="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
  static readonly XNamespace Document="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
  static readonly XNamespace Package="http://schemas.openxmlformats.org/package/2006/relationships";
  static readonly XNamespace Types="http://schemas.openxmlformats.org/package/2006/content-types";

  internal static void Save(string path,IDictionary<string,IEnumerable<object[]>> sheets) {
   string target=Path.GetFullPath(path);
   string temp=Path.Combine(Path.GetDirectoryName(target),".qlkpxlsx-"+Guid.NewGuid().ToString("N")+".tmp");
   try {
    using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
     using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true)) {
      var types=new XElement(Types+"Types",
       new XElement(Types+"Default",new XAttribute("Extension","rels"),new XAttribute("ContentType","application/vnd.openxmlformats-package.relationships+xml")),
       new XElement(Types+"Default",new XAttribute("Extension","xml"),new XAttribute("ContentType","application/xml")),
       Override("/xl/workbook.xml","sheet.main"));
      var sheetList=new XElement(Main+"sheets");
      var workbookRels=new XElement(Package+"Relationships");
      int index=0;
      foreach(var sheet in sheets) {
       int id=++index;
       string name="worksheets/sheet"+id+".xml";
       string rel="rId"+id;
       types.Add(Override("/xl/"+name,"worksheet"));
       workbookRels.Add(Relationship(rel,"worksheet",name));
       sheetList.Add(new XElement(Main+"sheet",new XAttribute("name",sheet.Key),new XAttribute("sheetId",id),new XAttribute(Document+"id",rel)));
       var rows=new XElement(Main+"sheetData");
       int line=0;
       foreach(var row in sheet.Value) {
        int rowNumber=++line;
        rows.Add(new XElement(Main+"row",new XAttribute("r",rowNumber),row.Select((value,column)=>Cell(value,column,rowNumber))));
       }
       Write(zip,"xl/"+name,new XElement(Main+"worksheet",rows));
      }
      Write(zip,"[Content_Types].xml",types);
      Write(zip,"_rels/.rels",new XElement(Package+"Relationships",Relationship("rId1","officeDocument","xl/workbook.xml")));
      Write(zip,"xl/workbook.xml",new XElement(Main+"workbook",sheetList));
      Write(zip,"xl/_rels/workbook.xml.rels",workbookRels);
     }
     stream.Flush(true);
    }
    File.Move(temp,target);
   } finally {if(File.Exists(temp))File.Delete(temp);}
  }
  static XElement Override(string path,string type) {
   return new XElement(Types+"Override",new XAttribute("PartName",path),
    new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml."+type+"+xml"));
  }
  static XElement Relationship(string id,string type,string target) {
   return new XElement(Package+"Relationship",new XAttribute("Id",id),
    new XAttribute("Type",Document.NamespaceName+"/"+type),new XAttribute("Target",target));
  }
  static XElement Cell(object value,int column,int row) {
   string letters="";
   for(int valueIndex=column+1;valueIndex>0;valueIndex=(valueIndex-1)/26)
    letters=(char)('A'+(valueIndex-1)%26)+letters;
   var position=new XAttribute("r",letters+row);
   if(value is long) return new XElement(Main+"c",position,new XElement(Main+"v",value));
   return new XElement(Main+"c",position,new XAttribute("t","inlineStr"),
    new XElement(Main+"is",new XElement(Main+"t",new XAttribute(XNamespace.Xml+"space","preserve"),value??"")));
  }
  static void Write(ZipArchive zip,string path,XElement xml) {
   using(var stream=zip.CreateEntry(path,CompressionLevel.Optimal).Open())new XDocument(xml).Save(stream);
  }
 }
}
