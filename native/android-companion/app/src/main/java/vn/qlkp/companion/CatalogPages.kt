package vn.qlkp.companion

internal data class CatalogPage(val rows:List<String>,val index:Int,val pageCount:Int,val matchCount:Int)

internal fun catalogPage(entries:List<String>,query:String,requestedIndex:Int):CatalogPage {
 val needle=query.trim()
 val matching=entries.filter { it.contains(needle,ignoreCase=true) }
 val pageSize=30
 val pageCount=(matching.size+pageSize-1)/pageSize
 val index=requestedIndex.coerceIn(0,(pageCount-1).coerceAtLeast(0))
 return CatalogPage(matching.drop(index*pageSize).take(pageSize),index,pageCount,matching.size)
}
