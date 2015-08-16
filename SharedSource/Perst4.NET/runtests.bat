set save_path=%path%
set path=bin;%path%
del *.dbs,*.map
tst\Simple\bin\debug\Simple
del *.dbs
tst\TestPerf\bin\debug\TestPerf
del *.dbs
tst\TestPerf\bin\debug\TestPerf inmemory
del *.dbs
tst\TestRegex\bin\debug\TestRegex
del *.dbs
tst\TestIndex\bin\debug\TestIndex
del *.dbs
tst\TestIndex\bin\debug\TestIndex altbtree
del *.dbs
tst\TestIndex\bin\debug\TestIndex altbtree serializable
del *.dbs
tst\TestIndex\bin\debug\TestIndex inmemory
del *.dbs
tst\TestIndex\bin\debug\TestIndex zip
del *.dbs,*.map
tst\TestIndex\bin\debug\TestIndex crypt
del *.dbs,*.map
tst\TestIndex\bin\debug\TestIndex zip crypt
tst\TestIndex\bin\debug\TestIndex zip crypt
del *.dbs
tst\TestKDTree\bin\debug\TestKDTree
tst\TestKDTree2\bin\debug\TestKDTree2
tst\TestNullable\bin\debug\TestNullable
del *.dbs
tst\TestMap\bin\debug\TestMap
del *.dbs
tst\TestMap\bin\debug\TestMap 100
tst\TestIndex2\bin\debug\TestIndex2
tst\TestEnumerator\bin\debug\TestEnumerator
del *.dbs
tst\TestEnumerator\bin\debug\TestEnumerator altbtree
tst\TestCompoundIndex\bin\debug\TestCompoundIndex
tst\TestRtree\bin\debug\TestRtree
tst\TestR2\bin\debug\TestR2
tst\TestTtree\bin\debug\TestTtree
tst\TestRaw\bin\debug\TestRaw
tst\TestRaw\bin\debug\TestRaw
tst\TestGC\bin\debug\TestGC
tst\TestGC\bin\debug\TestGC background
tst\TestGC\bin\debug\TestGC background altbtree
tst\TestConcur\bin\debug\TestConcur
tst\TestConcur\bin\debug\TestConcur pinned
tst\TestLockUpgrade\bin\debug\TestLockUpgrade
tst\TestLockUpgrade\bin\debug\TestLockUpgrade pinned
tst\TestServer\bin\debug\TestServer
tst\TestDbServer\bin\debug\TestDbServer
tst\TestXML\bin\debug\TestXML
tst\TestJSQL\bin\debug\TestJSQL
tst\TestJsqlJoin\bin\debug\TestJsqlJoin
tst\TestJsqlJoin\bin\debug\TestJsqlJoin
tst\TestCodeGenerator\bin\debug\TestCodeGenerator
tst\TestAutoIndices\bin\debug\TestAutoIndices
tst\TestBackup\bin\debug\TestBackup
tst\TestBlob\bin\debug\TestBlob large
tst\TestBlob\bin\debug\TestBlob large
del *.dbs
tst\TestBlob\bin\debug\TestBlob large zip crypt
tst\TestBlob\bin\debug\TestBlob large zip crypt
del *.dbs,*.map
tst\TestBlob\bin\debug\TestBlob large crypt
tst\TestBlob\bin\debug\TestBlob large crypt
tst\TestAlloc\bin\debug\TestAlloc
tst\TestAlloc\bin\debug\TestAlloc
tst\TestAlloc\bin\debug\TestAlloc
tst\TestTimeSeries\bin\debug\TestTimeSeries
tst\TestBit\bin\debug\TestBit
tst\TestBitmap\bin\debug\TestBitmap
tst\TestList\bin\debug\TestList
tst\TestList2\bin\debug\TestList2
tst\TestRndIndex\bin\debug\TestRndIndex
start tst\TestReplic\bin\debug\TestReplic master
tst\TestReplic\bin\debug\TestReplic slave
rem tst\TestLinq\bin\debug\TestLinq
tst\TestFullTextIndex\bin\debug\TestFullTextIndex
tst\TestFullTextIndex\bin\debug\TestFullTextIndex
tst\TestFullTextIndex\bin\debug\TestFullTextIndex reload
set path=%save_path%
