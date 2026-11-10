using System;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace QLKP.Desktop.Core {
 internal static class LocalQrImage {
  internal static BitmapSource Create(string payload) {
   var matrix=new QRCodeWriter().encode(payload,BarcodeFormat.QR_CODE,320,320);
   var pixels=new byte[matrix.Width*matrix.Height];
   for(int y=0;y<matrix.Height;y++)
    for(int x=0;x<matrix.Width;x++)pixels[y*matrix.Width+x]=matrix[x,y]?(byte)0:(byte)255;
   var bitmap=BitmapSource.Create(matrix.Width,matrix.Height,96,96,PixelFormats.Gray8,null,pixels,matrix.Width);
   bitmap.Freeze();return bitmap;
  }

  internal static string Read(string path) {
   using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
    if(stream.Length<1||stream.Length>10L*1024*1024)throw new InvalidDataException("Ảnh cần có kích thước tệp từ 1 byte đến 10 MB.");
    var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand);
    if(!(decoder is PngBitmapDecoder||decoder is JpegBitmapDecoder||decoder is BmpBitmapDecoder)||decoder.Frames.Count!=1)
     throw new InvalidDataException("Chọn một ảnh PNG, JPEG hoặc BMP.");
    var frame=decoder.Frames[0];
    int width=frame.PixelWidth,height=frame.PixelHeight;
    if(width<1||height<1||width>4096||height>4096||(long)width*height>12000000)
     throw new InvalidDataException("Ảnh vượt giới hạn 4.096 pixel mỗi chiều hoặc 12 triệu pixel.");
    var gray=new FormatConvertedBitmap(frame,PixelFormats.Gray8,null,0);
    var pixels=new byte[width*height];
    gray.CopyPixels(pixels,width,0);
    var source=new RGBLuminanceSource(pixels,width,height,RGBLuminanceSource.BitmapFormat.Gray8);
    var result=new QRCodeReader().decode(new BinaryBitmap(new HybridBinarizer(source)));
    if(result==null||String.IsNullOrWhiteSpace(result.Text))throw new InvalidDataException("Không đọc được mã QR từ ảnh.");
    return result.Text;
   }
  }

  internal static string ReadText(string path) {
   using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
    if(stream.Length<1||stream.Length>256)throw new InvalidDataException("Tệp mã QR phải từ 1 đến 256 byte.");
    using(var reader=new StreamReader(stream,new UTF8Encoding(false,true),false))
     return reader.ReadToEnd().TrimStart('\ufeff');
   }
  }

  internal static void Save(BitmapSource bitmap,string path) {
   if(bitmap==null)throw new InvalidOperationException("Hãy tạo mã QR trước khi lưu.");
   string target=Path.GetFullPath(path);
   string temp=Path.Combine(Path.GetDirectoryName(target),".qlkpqr-"+Guid.NewGuid().ToString("N")+".tmp");
   try {
    var encoder=new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))encoder.Save(file);
    File.Move(temp,target);
   } finally {if(File.Exists(temp))File.Delete(temp);}
  }
 }
}
