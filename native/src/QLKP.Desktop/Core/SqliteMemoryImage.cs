using System;
using System.Data.SQLite;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
namespace QLKP.Desktop.Core {
 // Pinned System.Data.SQLite 2.0.4 bridge. The provider owns its normally-opened
 // in-memory handle throughout deserialization; no shared-cache alias or disk file.
 internal sealed class SqliteMemoryImage : IDisposable {
  public SQLiteConnection Connection {get;private set;}
  static byte[] U(string s){return Encoding.UTF8.GetBytes(s+"\0");}
  public SqliteMemoryImage(byte[] image) {
   IntPtr buffer=IntPtr.Zero;
   try {
    Connection=VaultStorage.OpenPlain(":memory:",false,true);
    var lookup=typeof(SQLiteConnection).GetMethod("GetNativeHandle",BindingFlags.Static|BindingFlags.NonPublic);
    if(lookup==null)throw new NotSupportedException("Thiếu giao tiếp ảnh SQLite trong RAM.");
    object wrapper=lookup.Invoke(null,new object[]{Connection});
    var convert=wrapper.GetType().GetMethod("op_Implicit",BindingFlags.Static|BindingFlags.Public,null,new[]{wrapper.GetType()},null);
    if(convert==null||convert.ReturnType!=typeof(IntPtr))throw new NotSupportedException("Không đọc được handle SQLite trong RAM.");
    IntPtr handle=(IntPtr)convert.Invoke(null,new[]{wrapper});
    if(handle==IntPtr.Zero)throw new InvalidOperationException("Ảnh SQLite chưa mở.");
    buffer=sqlite3_malloc64((ulong)image.Length);if(buffer==IntPtr.Zero)throw new OutOfMemoryException();
    Marshal.Copy(image,0,buffer,image.Length);
    if(sqlite3_deserialize(handle,U("main"),buffer,image.Length,image.Length,5)!=0)throw new InvalidOperationException("Không đọc được ảnh SQLite cũ.");
    buffer=IntPtr.Zero;
    using(var q=new SQLiteCommand("SELECT count(*) FROM sqlite_master WHERE type='table'",Connection))if(Convert.ToInt32(q.ExecuteScalar())==0)throw new InvalidOperationException("Ảnh SQLite trong RAM không có schema.");
   }catch{if(buffer!=IntPtr.Zero)sqlite3_free(buffer);Dispose();throw;}
  }
  public void Dispose(){if(Connection!=null){Connection.Dispose();Connection=null;}}
  [DllImport("e_sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern IntPtr sqlite3_malloc64(ulong size);
  [DllImport("e_sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern void sqlite3_free(IntPtr buffer);
  [DllImport("e_sqlite3",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_deserialize(IntPtr db,byte[] schema,IntPtr bytes,long length,long capacity,uint flags);
 }
}
