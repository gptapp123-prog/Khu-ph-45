using System;
using System.Data.SQLite;
using System.Globalization;
using System.Security.Cryptography;

namespace QLKP.Desktop.Core {
 public static partial class DataStore {
  static bool PasswordMatches(string secret,string hash,string salt) {
   return !String.IsNullOrEmpty(secret) &&
    EqualBytes(Convert.FromBase64String(hash),Convert.FromBase64String(Hash(secret,Convert.FromBase64String(salt))));
  }

  static bool IsLoginBlocked(SQLiteConnection c,SQLiteTransaction tx,string id) {
   var blocked=Convert.ToString(Scalar(c,tx,"SELECT blocked_until_utc FROM login_throttle WHERE account_id=@id",P("@id",id)));
   DateTime until;
   if(!DateTime.TryParse(blocked,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out until))return false;
   if(until.ToUniversalTime()>DateTime.UtcNow)return true;
   ClearLoginThrottle(c,tx,id);
   return false;
  }

  static void ClearLoginThrottle(SQLiteConnection c,SQLiteTransaction tx,string id) {
   Exec(c,tx,"DELETE FROM login_throttle WHERE account_id=@id",P("@id",id));
  }

  static void RecordLoginFailure(SQLiteConnection c,SQLiteTransaction tx,string id,string eventCode) {
   Exec(c,tx,"INSERT OR IGNORE INTO login_throttle(account_id,failed_count,updated_utc) VALUES(@id,0,@t)",P("@id",id),P("@t",Now));
   Exec(c,tx,@"UPDATE login_throttle SET failed_count=failed_count+1,updated_utc=@t,
    blocked_until_utc=CASE WHEN failed_count+1>=5 THEN @until ELSE NULL END WHERE account_id=@id",
    P("@id",id),P("@t",Now),P("@until",DateTime.UtcNow.AddMinutes(15).ToString("o",CultureInfo.InvariantCulture)));
   Audit(c,tx,id,eventCode,"account",id);
  }

  static void VerifyCurrentPassword(SQLiteConnection c,SQLiteTransaction tx,Session actor,string password) {
   Require(c,tx,actor,"I01","read",actor==null?null:actor.AreaId);
   if(IsLoginBlocked(c,tx,actor.Id))throw new InvalidOperationException("Xác minh đang tạm khóa. Vui lòng thử lại sau.");
   string hash=Convert.ToString(Scalar(c,tx,"SELECT hash_b64 FROM accounts WHERE id=@id",P("@id",actor.Id)));
   string salt=Convert.ToString(Scalar(c,tx,"SELECT salt_b64 FROM accounts WHERE id=@id",P("@id",actor.Id)));
   if(!PasswordMatches(password,hash,salt)) {
    RecordLoginFailure(c,tx,actor.Id,"PASSWORD_CONFIRMATION_DENIED");
    tx.Commit();
    throw new UnauthorizedAccessException("Mật khẩu hiện tại không đúng.");
   }
   ClearLoginThrottle(c,tx,actor.Id);
  }

  static void ReplacePassword(SQLiteConnection c,SQLiteTransaction tx,string actorId,string targetId,string password,string eventCode) {
   var secret=NewPassword(password);
   Exec(c,tx,"UPDATE accounts SET hash_b64=@h,salt_b64=@s,policy_version=policy_version+1 WHERE id=@id",
    P("@h",secret.Item1),P("@s",secret.Item2),P("@id",targetId));
   ClearLoginThrottle(c,tx,targetId);
   Audit(c,tx,actorId,eventCode,"account",targetId);
  }

  public static void ChangeOwnPassword(Session actor,string currentPassword,string newPassword) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    VerifyCurrentPassword(c,tx,actor,currentPassword);
    if(String.Equals(currentPassword,newPassword,StringComparison.Ordinal))
     throw new InvalidOperationException("Mật khẩu mới phải khác mật khẩu hiện tại.");
    ReplacePassword(c,tx,actor.Id,actor.Id,newPassword,"CHANGE_OWN_PASSWORD");
    tx.Commit();
   }
  }

  public static void ResetSubordinatePassword(Session actor,string targetId,string adminPassword,string newPassword) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    CheckTarget(c,tx,actor,targetId);
    VerifyCurrentPassword(c,tx,actor,adminPassword);
    ReplacePassword(c,tx,actor.Id,targetId,newPassword,"RESET_SUBORDINATE_PASSWORD");
    tx.Commit();
   }
  }

  static string ReplaceRecoveryCode(SQLiteConnection c,SQLiteTransaction tx,string rootId) {
   var bytes=new byte[16];
   using(var random=RandomNumberGenerator.Create())random.GetBytes(bytes);
   string code=BitConverter.ToString(bytes).Replace("-","");
   Array.Clear(bytes,0,bytes.Length);
   var secret=NewPassword(code);
   Exec(c,tx,"INSERT OR REPLACE INTO account_recovery(account_id,hash_b64,salt_b64) VALUES(@id,@h,@s)",
    P("@id",rootId),P("@h",secret.Item1),P("@s",secret.Item2));
   return code;
  }

  public static string IssueRootRecoveryCode(Session actor,string currentPassword) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    if(actor==null||actor.Role!="ROOT_ADMIN")throw new UnauthorizedAccessException("Chỉ quản trị hệ thống được tạo mã phục hồi.");
    VerifyCurrentPassword(c,tx,actor,currentPassword);
    string code=ReplaceRecoveryCode(c,tx,actor.Id);
    Audit(c,tx,actor.Id,"ISSUE_ROOT_RECOVERY_CODE","account",actor.Id);
    tx.Commit();
    return code;
   }
  }

  public static string RecoverRootPassword(string username,string recoveryCode,string newPassword) {
   if(String.IsNullOrWhiteSpace(username)||username.Length>128||String.IsNullOrWhiteSpace(recoveryCode))return null;
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    string id=null,hash=null,salt=null;
    using(var cmd=new SQLiteCommand(@"SELECT a.id,r.hash_b64,r.salt_b64 FROM accounts a JOIN account_recovery r ON r.account_id=a.id
     WHERE a.username=@u COLLATE NOCASE AND a.role='ROOT_ADMIN' AND a.active=1",c,tx)) {
     cmd.Parameters.Add(P("@u",username.Trim()));
     using(var reader=cmd.ExecuteReader())if(reader.Read()){id=reader.GetString(0);hash=reader.GetString(1);salt=reader.GetString(2);}
    }
    if(id==null||IsLoginBlocked(c,tx,id)){tx.Commit();return null;}
    var code=recoveryCode.Trim();
    if(code.Length!=32||!PasswordMatches(code.ToUpperInvariant(),hash,salt)) {
     RecordLoginFailure(c,tx,id,"ROOT_RECOVERY_DENIED");
     tx.Commit();return null;
    }
    ReplacePassword(c,tx,id,id,newPassword,"RECOVER_ROOT_PASSWORD");
    string replacement=ReplaceRecoveryCode(c,tx,id);
    tx.Commit();
    return replacement;
   }
  }
 }
}
