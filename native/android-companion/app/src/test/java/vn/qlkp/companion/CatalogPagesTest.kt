package vn.qlkp.companion

import org.junit.Assert.assertEquals
import org.junit.Test

class CatalogPagesTest {
 @Test fun lastPageAndFilteredRowsRemainReachable() {
  val entries=(1..1000).map { "LOAI_NHA/MA_$it: Nhãn $it" }
  val last=catalogPage(entries,"",99)
  assertEquals(33,last.index)
  assertEquals(34,last.pageCount)
  assertEquals(entries.last(),last.rows.last())
  assertEquals(10,last.rows.size)
  val result=catalogPage(entries,"  ma_1000  ",12)
  assertEquals(0,result.index)
  assertEquals(1,result.matchCount)
  assertEquals(listOf(entries.last()),result.rows)
  assertEquals(0,catalogPage(entries,"vắng mặt",2).pageCount)
 }
}
