using System;
using System.Collections.Generic;
using System.Data.SQLite;
namespace QLKP.Desktop.Core {
 public sealed class LinkCandidate {
  public string Id {get;set;} public string Feature {get;set;} public string Display {get;set;}
 }
 public static partial class DataStore {
  // Lookup is permission-checked on the TARGET feature; no cross-area or silent name match.
  // Backward-compatible single-page lookup: paged reader is the authority.
  public static List<LinkCandidate> FindLinkCandidates(Session s,string feature,string keyword) {
   return GetLinkCandidatesPage(s,feature,keyword,0,30).Items;
  }
  public static RecordPage<LinkCandidate> GetLinkCandidatesPage(Session s,string feature,string keyword,
    int pageIndex,int pageSize=30) {
   if(Array.IndexOf(ModuleCatalog.LinkedFeatures(),feature)<0)
    throw new InvalidOperationException("Chưa hỗ trợ kiểu hồ sơ liên kết: "+feature);
   if(pageIndex<0 || pageSize<1 || pageSize>100)throw new InvalidOperationException("Trang hoặc số dòng tra cứu không hợp lệ.");
   using(var c=Open())using(var tx=c.BeginTransaction()) {
    Require(c,tx,s,feature,"read",s==null?null:s.AreaId);
    string from,filter,select,order;
    if(feature=="B01") {
     from="households";select="id,code||' · '||address";order="code,id";
     filter="area_id=@a AND (code LIKE @q ESCAPE '\\' OR search_address LIKE @q ESCAPE '\\')";
    } else if(feature=="B02") {
     from="people";select="id,full_name";order="full_name,id";
     filter="area_id=@a AND search_name LIKE @q ESCAPE '\\'";
    } else {
     from="module_records";select="id,record_code||' · '||title";order="record_code,id";
     filter="area_id=@a AND feature=@f AND (record_code LIKE @q ESCAPE '\\' OR search_title LIKE @q ESCAPE '\\')";
    }
    string q=SearchPattern(NormalizeSearch(keyword));
    long total=Convert.ToInt64(Scalar(c,tx,"SELECT COUNT(*) FROM "+from+" WHERE "+filter,
     P("@a",s.AreaId),P("@f",feature),P("@q",q)));
    long pages=total==0?0:(total-1)/pageSize+1;
    if(pageIndex>=pages)pageIndex=(int)Math.Max(0,pages-1);
    var result=new RecordPage<LinkCandidate>{PageIndex=pageIndex,PageSize=pageSize,TotalCount=total};
    using(var cmd=new SQLiteCommand("SELECT "+select+" FROM "+from+" WHERE "+filter+
      " ORDER BY "+order+" LIMIT @limit OFFSET @offset",c,tx)) {
     cmd.Parameters.AddRange(new[]{P("@a",s.AreaId),P("@f",feature),P("@q",q),
       P("@limit",pageSize),P("@offset",pageIndex*pageSize)});
     using(var r=cmd.ExecuteReader())while(r.Read()) {
      string id=r.GetString(0);
      result.Items.Add(new LinkCandidate{Id=id,Feature=feature,
       Display=r.GetString(1)+" · ID "+id.Substring(0,Math.Min(10,id.Length))});
     }
    }
    tx.Commit();return result;
   }
  }
  public static int GetModulePageIndexById(Session s,string feature,string id,int pageSize=50) {
   if(pageSize<1||pageSize>200)throw new InvalidOperationException("Kích thước trang không hợp lệ.");
   GetModuleRecord(s,feature,id); // Re-validate the exact ID and scope.
   using(var c=Open()) {
    Require(c,null,s,feature,"read",s.AreaId);
    string stamp=Convert.ToString(Scalar(c,null,"SELECT updated_utc FROM module_records WHERE id=@id AND feature=@f AND area_id=@a",
     P("@id",id),P("@f",feature),P("@a",s.AreaId)));
    long offset=Convert.ToInt64(Scalar(c,null,@"SELECT COUNT(*) FROM module_records WHERE area_id=@a AND feature=@f AND
       (updated_utc>@utc OR (updated_utc=@utc AND id>@id))",P("@a",s.AreaId),P("@f",feature),P("@utc",stamp),P("@id",id)));
    return (int)(offset/pageSize);
   }
  }
 }
}
