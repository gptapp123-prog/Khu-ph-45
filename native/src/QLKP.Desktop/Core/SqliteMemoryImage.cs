using System;
using System.Data.SQLite;
namespace QLKP.Desktop.Core {
 // The pinned provider deserializes the legacy database directly into its own
 // private in-memory connection. No plaintext temporary database is written.
 internal sealed class SqliteMemoryImage : IDisposable {
  public SQLiteConnection Connection {get;private set;}
  public SqliteMemoryImage(byte[] image) {
   try {
    Connection=VaultStorage.OpenPlain(":memory:",false,true);
    Connection.Deserialize("main",image,true);
    using(var q=new SQLiteCommand("SELECT count(*) FROM sqlite_master WHERE type='table'",Connection))if(Convert.ToInt32(q.ExecuteScalar())==0)throw new InvalidOperationException("Ảnh SQLite trong RAM không có schema.");
   }catch{Dispose();throw;}
  }
  public void Dispose(){if(Connection!=null){Connection.Dispose();Connection=null;}}
 }
}
