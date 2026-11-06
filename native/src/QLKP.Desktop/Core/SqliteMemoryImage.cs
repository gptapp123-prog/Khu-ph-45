using System;
using System.Data.SQLite;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
namespace QLKP.Desktop.Core {
 // Legacy V1 backups are decoded into RAM, never a plaintext temporary disk file.
 internal sealed class SqliteMemoryImage : IDisposable {
  IntPtr handle;public SQLiteConnection Connection {get;private set;}
  static byte[] U(string s){return Encoding.UTF8.GetBytes(s+"\0");}
  public SqliteMemoryImage(byte[] image) {
   IntPtr buffer=IntPtr.Zero;
   try {
    if(sqlite3_open_v2(U(":memory:"),out handle,6,IntPtr.Zero)!=0)throw new InvalidOperationException("Không mở được ảnh SQLite trong RAM.");
    buffer=sqlite3_malloc64((ulong)image.Length);if(buffer==IntPtr.Zero)throw new OutOfMemoryException();
    Marshal.Copy(image,0,buffer,image.Length);
    if(sqlite3_deserialize(handle,U("main"),buffer,image.Length,image.Length,5)!=0)throw new InvalidOperationException("Không đọc được ảnh SQLite cũ.");
    buffer=IntPtr.Zero;
    var ctor=typeof(SQLiteConnection).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(IntPtr),typeof(string),typeof(bool)},null);
    if(ctor==null)throw new NotSupportedException("Thư viện SQLite không hỗ trợ ảnh phục hồi trong RAM.");
    Connection=(SQLiteConnection)ctor.Invoke(new object[]{handle,":memory:",false});
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
