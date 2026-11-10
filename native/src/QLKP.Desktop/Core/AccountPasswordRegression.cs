using System;
using System.IO;

namespace QLKP.Desktop.Core {
 public static partial class DataStore {
  internal static void RunAccountPasswordRegression(TestRunRecorder run,Session root,string rootPassword) {
   run.BeginStep("ACCOUNT_PASSWORD_CHANGE_AND_ADMIN_RESET");
   string initial="Initial-"+Id(), changed="Changed-"+Id(), reset="Reset-"+Id();
   CreateAccount(root,"password_admin",initial,null);
   var admin=Login("password_admin",initial);
   CreateAccount(admin,"password_operator",initial,new[]{"B01:read"});
   var op=Login("password_operator",initial);
   bool denied=false;
   try { ChangeOwnPassword(op,"invalid",changed); }
   catch(UnauthorizedAccessException) { denied=true; }
   if(!denied)throw new InvalidDataException("Changed password with invalid proof.");
   ChangeOwnPassword(op,initial,changed);
   if(Can(op,"B01","read")||Login("password_operator",initial)!=null||Login("password_operator",changed)==null)
    throw new InvalidDataException("Password change did not revoke old credentials/session.");
   op=Login("password_operator",changed);
   denied=false;
   try { ResetSubordinatePassword(admin,root.Id,initial,reset); }
   catch(UnauthorizedAccessException) { denied=true; }
   if(!denied)throw new InvalidDataException("Local admin reset root password.");
   ResetSubordinatePassword(admin,op.Id,initial,reset);
   if(Can(op,"B01","read")||Login("password_operator",changed)!=null||Login("password_operator",reset)==null)
    throw new InvalidDataException("Admin reset did not replace credentials and revoke session.");
   ResetSubordinatePassword(root,admin.Id,rootPassword,reset);
   if(Can(admin,"I01","manage")||Login("password_admin",reset)==null)
    throw new InvalidDataException("Root reset did not replace local admin credentials.");

   run.BeginStep("ACCOUNT_ROOT_RECOVERY_ROTATION_AND_REUSE");
   if(RecoverRootPassword(root.Username,"unknown",reset)!=null)
    throw new InvalidDataException("Root recovery succeeded without enrollment.");
   string oldCode=IssueRootRecoveryCode(root,rootPassword);
   string code=IssueRootRecoveryCode(root,rootPassword);
   if(code.Length!=32||oldCode==code||RecoverRootPassword(root.Username,oldCode,reset)!=null)
    throw new InvalidDataException("Reissued recovery code did not invalidate the original.");
   string replacement=RecoverRootPassword(root.Username,code,reset);
   if(replacement==null||replacement==code||Can(root,"I01","manage")||Login(root.Username,rootPassword)!=null||
      Login(root.Username,reset)==null||RecoverRootPassword(root.Username,code,changed)!=null)
    throw new InvalidDataException("Recovery did not revoke old credentials/code.");
   if(RecoverRootPassword(root.Username,replacement,changed)==null||Login(root.Username,changed)==null)
    throw new InvalidDataException("Replacement recovery code unusable.");
  }
 }
}
