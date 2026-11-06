using System;
using System.Data.SQLite;
using System.Runtime.InteropServices;
using System.Text;
namespace QLKP.Desktop.Core {
 // Legacy V1 backups are decoded into RAM, never a plaintext temporary disk file.
 internal sealed class SqliteMemoryImage : IDisposable {
  IntPtr handle;public SQLiteConnection Connection {get;private set;}
  static byte[] U(string s){return Encoding.UTF8.GetBytes(s+"\0");}
  public SqliteMemoryImage(byte[] image) {
   string uri="file:qlkp-memory-"+Guid.NewGuid().ToString("N")+"?mode=memory&cache=shared";
   IntPtr buffer=IntPtr.Zero;
   try {
    if(sqlite3_open_v2(U(uri),out handle,0x20000|0x40|6,IntPtr.Zero)!=0)throw new InvalidOperationException("Không mở được ảnh SQLite trong RAM.");
    buffer=sqlite3_malloc64((ulong)image.Length);if(buffer==IntPtr.Zero)throw new OutOfMemoryException();
    Marshal.Copy(image,0,buffer,image.Length);
    if(sqlite3_deserialize(handle,U("main"),buffer,image.Length,image.Length,5)!=0)throw new InvalidOperationException("Không đọc được ảnh SQLite cũ.");
    buffer=IntPtr.Zero;
    Connection=new SQLiteConnection("FullUri="+uri+";Version=3;Pooling=False;");Connection.Open();
    using(var q=new SQLiteCommand("PRAGMA trusted_schema=OFF;PRAGMA temp_store=MEMORY;",Connection))q.ExecuteNonQuery();
    using(var q=new SQLiteCommand("SELECT count(*) FROM sqlite_master WHERE type='table'",Connection))if(Convert.ToInt32(q.ExecuteScalar())==0)throw new InvalidOperationException("Ảnh SQLite trong RAM không có schema.");
   }catch{if(buffer!=IntPtr.Zero)sqlite3_free(buffer);Dispose();throw;}
  }
  public void Dispose(){if(Connection!=null){Connection.Dispose();Connection=null;}if(handle!=IntPtr.Zero){sqlite3_close_v2(handle);handle=IntPtr.Zero;}}
  [DllImport("e_sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] filename,out IntPtr db,int flags,IntPtr vfs);
  [DllImport("e_sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_malloc64(ulong size);
  [DllImport("e_sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern void sqlite3_free(IntPtr buffer);
  [DllImport("e_sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_deserialize(IntPtr db,byte[] schema,IntPtr bytes,long length,long capacity,uint flags);
  [DllImport("e_sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close_v2(IntPtr db);
 }
}
