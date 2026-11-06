using System;
using System.Data.SQLite;
namespace QLKP.Desktop.Core {
 public static partial class DataStore {
  // Explicit administrator action; never called by Initialize or login. Existing edits are preserved.
  public static int EnsureReferenceCatalog(Session actor) {
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,actor,"I07","manage",actor==null?null:actor.AreaId);
    string scope=CatalogScope(actor);int inserted=0;
    foreach(var row in OfflineCatalogTransfer.ReferenceEntries()) {
     using(var cmd=new SQLiteCommand(@"INSERT INTO shared_catalog_items
      (id,scope_area,category_code,item_code,item_label,search_label,details,active,version,changed_by,updated_utc)
      SELECT @id,@scope,@category,@code,@label,@search,'',1,1,@actor,@utc
      WHERE NOT EXISTS (SELECT 1 FROM shared_catalog_items WHERE scope_area=@scope AND category_code=@category AND item_code=@code)",c,tx)) {
      cmd.Parameters.AddRange(new[]{P("@id",Id()),P("@scope",scope),P("@category",row.Category),P("@code",row.Code),
       P("@label",row.Label),P("@search",NormalizeSearch(row.Label)),P("@actor",actor.Id),P("@utc",Now)});
      inserted+=cmd.ExecuteNonQuery();
     }
    }
    Audit(c,tx,actor.Id,"I07_REFERENCE_CATALOG_ENSURED","catalog",scope);tx.Commit();return inserted;
   }
  }
 }
}
