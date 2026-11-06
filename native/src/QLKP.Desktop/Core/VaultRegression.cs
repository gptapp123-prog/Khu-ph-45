using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace QLKP.Desktop.Core {
 internal static class VaultRegression {
  internal static void Run(TestRunRecorder run,Session root) {
   string dir=DataStore.DataFolder;
   var key=VaultStorage.CurrentKey();string expected;
   try {
    run.BeginStep("VAULT_REOPEN_AND_NO_PLAINTEXT");
    if(VaultStorage.IsPlainSqlite(DataStore.DatabasePath))throw new InvalidDataException("Database header remains plaintext.");
    using(var c=DataStore.Open()){VaultStorage.Check(c);expected=VaultStorage.Fingerprint(c);}
    bool noKeyDenied=false;try{using(var c=new SQLiteConnection("Data Source="+DataStore.DatabasePath+";Version=3;Read Only=True;Pooling=False;")) {c.Open();using(var q=new SQLiteCommand("SELECT count(*) FROM accounts",c))q.ExecuteScalar();}}catch(SQLiteException){noKeyDenied=true;}
    if(!noKeyDenied)throw new InvalidDataException("Encrypted database read without key.");
    run.BeginStep("VAULT_WRONG_KEY_AND_TAMPER_DENIED");
    var wrong=(byte[])key.Clone();wrong[0]^=1;bool denied=false;
    try{using(var c=VaultStorage.OpenEncrypted(DataStore.DatabasePath,wrong,true))VaultStorage.Check(c);}catch(SQLiteException){denied=true;}finally{Array.Clear(wrong,0,wrong.Length);}
    if(!denied)throw new InvalidDataException("Wrong key accepted.");
    string tampered=Path.Combine(dir,"tamper.qlkpvault");File.Copy(DataStore.DatabasePath,tampered);
    try{using(var f=new FileStream(tampered,FileMode.Open,FileAccess.ReadWrite)){f.Position=200;int b=f.ReadByte();f.Position=200;f.WriteByte((byte)(b^1));}denied=false;try{using(var c=VaultStorage.OpenEncrypted(tampered,key,true))VaultStorage.Check(c);}catch(SQLiteException){denied=true;}if(!denied)throw new InvalidDataException("Tampered page accepted.");}finally{File.Delete(tampered);}
    run.BeginStep("VAULT_MISSING_DPAPI_KEY_FAILS_CLOSED");
    string held=VaultStorage.KeyPath+".held";File.Move(VaultStorage.KeyPath,held);
    try{denied=false;try{VaultStorage.EnsureCurrent();}catch(CryptographicException){denied=true;}if(!denied||File.Exists(VaultStorage.KeyPath))throw new InvalidDataException("Missing key replaced automatically.");}finally{File.Move(held,VaultStorage.KeyPath);}
    run.BeginStep("VAULT_LEGACY_PLAINTEXT_MIGRATION");
    string legacy=Path.Combine(dir,"legacy-fixture.db");byte[] legacyBytes;
    using(var src=DataStore.Open())using(var dst=VaultStorage.OpenPlain(legacy,false,true))VaultStorage.CopyTrusted(src,dst);
    if(!VaultStorage.IsPlainSqlite(legacy))throw new InvalidDataException("Plaintext migration fixture not created.");
    legacyBytes=File.ReadAllBytes(legacy);File.Replace(legacy,DataStore.DatabasePath,null);
    VaultStorage.EnsureCurrent();
    using(var c=DataStore.Open())if(VaultStorage.Fingerprint(c)!=expected)throw new InvalidDataException("Migration changed logical records.");
    if(VaultStorage.IsPlainSqlite(DataStore.DatabasePath))throw new InvalidDataException("Migration did not encrypt.");
    if(!Directory.GetFiles(Path.Combine(dir,"maintenance"),"before-encryption-*.qlkpvault").Any())throw new InvalidDataException("Migration recovery snapshot absent.");
    run.BeginStep("VAULT_LEGACY_V1_BACKUP_IN_MEMORY");
    string v1=Path.Combine(dir,"legacy.qlkpbak"),phrase="T-"+Guid.NewGuid().ToString("N")+"!";
    try{EncryptedBackup.WriteLegacyFixture(v1,phrase,legacyBytes);}finally{Array.Clear(legacyBytes,0,legacyBytes.Length);}
    EncryptedBackup.Validate(root,v1,phrase);
    run.BeginStep("VAULT_PORTABLE_V2_REKEY");
    string v2=Path.Combine(dir,"portable.qlkpbak");EncryptedBackup.Create(root,v2,phrase);
    byte[] original=File.ReadAllBytes(VaultStorage.KeyPath),other=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(other);
    try {
     var wrapped=ProtectedData.Protect(other,Encoding.UTF8.GetBytes("QLKP|DATABASE|DPAPI|V1"),DataProtectionScope.CurrentUser);
     File.WriteAllBytes(VaultStorage.KeyPath,Encoding.ASCII.GetBytes("QLKPDK01").Concat(wrapped).ToArray());
     string stage=EncryptedBackup.Materialize(v2,phrase);try{using(var c=VaultStorage.OpenEncrypted(stage,other,true))if(VaultStorage.Fingerprint(c)!=expected)throw new InvalidDataException("Portable rekey changed data.");}finally{File.Delete(stage);}
    }finally{File.WriteAllBytes(VaultStorage.KeyPath,original);Array.Clear(original,0,original.Length);Array.Clear(other,0,other.Length);}
   }finally{Array.Clear(key,0,key.Length);}
  }
 }
}
