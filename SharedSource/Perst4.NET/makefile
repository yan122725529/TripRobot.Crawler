!IFNDEF WINCE_CF10_HOME
WINCE_CF10_HOME=C:\Program Files\Microsoft.NET\SDK\CompactFramework\v2.0\WindowsCE
!ENDIF
!IFNDEF WINCE_CF20_HOME
WINCE_CF20_HOME=C:\Program Files\Microsoft.NET\SDK\CompactFramework\v2.0\WindowsCE
!ENDIF
!IFNDEF WINCE_CF35_HOME
WINCE_CF35_HOME=C:\Program Files\Microsoft.NET\SDK\CompactFramework\v3.5\WindowsCE
!ENDIF
!IFNDEF SILVERLIGHT_HOME
SILVERLIGHT_HOME=c:\Program Files\Reference Assemblies\Microsoft\Framework\Silverlight\v3.0
!ENDIF



!IFNDEF DXROOT
DXROOT=C:\Program Files\Sandcastle\
!ENDIF

!IFNDEF TARGET
TARGET=NET_2.0
!ENDIF

!IFNDEF DEBUG
DEBUG=1
!ENDIF

!IFNDEF OPTIMIZE
OPTIMIZE=0
!ENDIF

CSC_FLAGS=/w:2 /nologo /unsafe- /checked- 
CSC=csc
GENDOC=

!IF "$(TARGET)" == "NET_2.0"
CSC=C:\WINDOWS\Microsoft.NET\Framework\v2.0.50727\Csc.exe
DEFS=NET_FRAMEWORK_20
LIBRARY=bin\\PerstNet20.dll
GENDOC=/doc:doc\perst.xml
!ELSEIF "$(TARGET)" == "NET_3.5"
CSC=C:\WINDOWS\Microsoft.NET\Framework\v3.5\Csc.exe
DEFS=NET_FRAMEWORK_20,NET_FRAMEWORK_35
LIBRARY=bin\\PerstNet35.dll
!ELSEIF "$(TARGET)" == "NET_4.0"
CSC=C:\WINDOWS\Microsoft.NET\Framework\v4.0.30319\Csc.exe
DEFS=NET_FRAMEWORK_20,NET_FRAMEWORK_35
LIBRARY=bin\\PerstNet40.dll
!ELSEIF "$(TARGET)" == "NET_1.0"
DEFS=NET_FRAMEWORK_10
LIBRARY=bin\\PerstNet10.dll
!ELSEIF "$(TARGET)" == "GENERICS"
DEFS=USE_GENERICS,NET_FRAMEWORK_20
LIBRARY=bin\\PerstNetGenerics.dll
!ELSEIF "$(TARGET)" == "COMPACT_NET_3.5"
DEFS=COMPACT_NET_FRAMEWORK,NET_FRAMEWORK_20,COMPACT_NET_FRAMEWORK_35
CSC_FLAGS=$(CSC_FLAGS) /noconfig /nostdlib+ /r:"$(WINCE_CF35_HOME)\mscorlib.dll" /r:"$(WINCE_CF35_HOME)\System.dll" 
LIBRARY=bin\\PerstCompactNet35.dll
!ELSEIF "$(TARGET)" == "COMPACT_NET_2.0"
DEFS=COMPACT_NET_FRAMEWORK,NET_FRAMEWORK_20
CSC_FLAGS=$(CSC_FLAGS) /noconfig /nostdlib+ /r:"$(WINCE_CF20_HOME)\mscorlib.dll" /r:"$(WINCE_CF20_HOME)\System.dll" 
LIBRARY=bin\\PerstCompactNet20.dll
!ELSEIF "$(TARGET)" == "COMPACT_NET_1.0"
DEFS=COMPACT_NET_FRAMEWORK
CSC_FLAGS=$(CSC_FLAGS) /noconfig /nostdlib+ /r:"$(WINCE_CF10_HOME)\mscorlib.dll" /r:"$(WINCE_CF10_HOME)\System.dll" 
LIBRARY=bin\\PerstCompactNet10.dll
!ELSEIF "$(TARGET)" == "SILVERLIGHT_GENERICS"
DEFS=NET_FRAMEWORK_20,NET_FRAMEWORK_35,SILVERLIGHT,USE_GENERICS
CSC_FLAGS=$(CSC_FLAGS) /noconfig /nostdlib+ /r:"$(SILVERLIGHT_HOME)\mscorlib.dll" /r:"$(SILVERLIGHT_HOME)\System.Core.dll" /r:"$(SILVERLIGHT_HOME)\system.dll" /r:"$(SILVERLIGHT_HOME)\System.Net.dll" 
LIBRARY=bin\\PerstSilverlightGenerics.dll
!ELSEIF "$(TARGET)" == "SILVERLIGHT"
DEFS=NET_FRAMEWORK_20,NET_FRAMEWORK_35,SILVERLIGHT
CSC_FLAGS=$(CSC_FLAGS) /noconfig /nostdlib+ /r:"$(SILVERLIGHT_HOME)\mscorlib.dll" /r:"$(SILVERLIGHT_HOME)\System.Core.dll" /r:"$(SILVERLIGHT_HOME)\system.dll" /r:"$(SILVERLIGHT_HOME)\System.Net.dll" 
LIBRARY=bin\\PerstSilverlight.dll
!ELSE
!ERROR Unsupported TARGET, specify one of NET_3.5, NET_2.0, NET_1.0, GENERICS, COMPACT_NET_1.0, COMPACT_NET_2.0
!ENDIF

!IF $(DEBUG)
DEFS=$(DEFS),DEBUG
CSC_FLAGS=$(CSC_FLAGS) /debug+
!ENDIF

!IF $(OPTIMIZE)
CSC_FLAGS=$(CSC_FLAGS) /optimize+
!ENDIF

COMPILER=$(CSC) $(CSC_FLAGS) /d:$(DEFS)

!IF "$(TARGET)" == "COMPACT_NET_1.0" || "$(TARGET)" == "COMPACT_NET_2.0"

EXAMPLES=tst/Simple/bin/debug/Simple.exe tst/TestIndex/bin/debug/TestIndex.exe tst/TestPerf/bin/debug/TestPerf.exe tst/TestProp/bin/debug/TestProp.exe tst/TestKDTree/bin/debug/TestKDTree.exe tst/TestKDTree2/bin/debug/TestKDTree2.exe tst/TestMap/bin/debug/TestMap.exe tst/TestList2/bin/debug/TestList2.exe tst/TestRndIndex/bin/debug/TestRndIndex.exe tst/Guess/bin/debug/Guess.exe tst/TestXML/bin/debug/TestXML.exe tst/TestLink/bin/debug/TestLink.exe tst/TestConcur/bin/debug/TestConcur.exe tst/TestServer/bin/debug/TestServer.exe tst/TestGC/bin/debug/TestGC.exe tst/TestRtree/bin/debug/TestRtree.exe tst/TestSSD/bin/debug/TestSSD.exe tst/TestCompoundIndex/bin/debug/TestCompoundIndex.exe tst/TestEnumerator/bin/debug/TestEnumerator.exe tst/TestTtree/bin/debug/TestTtree.exe tst/TestBlob/bin/debug/TestBlob.exe tst/TestAlloc/bin/debug/TestAlloc.exe tst/TestLeak/bin/debug/TestLeak.exe tst/TestBackup/bin/debug/TestBackup.exe tst/TestTimeSeries/bin/debug/TestTimeSeries.exe tst/TestSOD/bin/debug/TestSOD.exe tst/TestIndex2/bin/debug/TestIndex2.exe tst/TestR2/bin/debug/TestR2.exe tst/TestRaw/bin/debug/TestRaw.exe tst/IpCountry/bin/debug/IpCountry.exe tst/TestBit/bin/debug/TestBit.exe tst/TestBitmap/bin/debug/TestBitmap.exe tst/TestJSQL/bin/debug/TestJSQL.exe tst/TestIndexCE/bin/debug/TestIndexCE.exe tst/TestFullTextIndex/bin/debug/TestFullTextIndex.exe tst/SearchEngine/bin/debug/SearchEngine.exe tst/TestLockUpgrade/bin/debug/TestLockUpgrade.exe tst/TestRegex/bin/debug/TestRegex.exe

SOURCES=src\AssemblyInfo.cs src\ISelfSerializable.cs src\impl\MemoryReader.cs src\MultidimensionalIndex.cs src\MultidimensionalComparator.cs src\BitIndex.cs src\Blob.cs src\ClassLoader.cs src\CompileError.cs src\CodeGeneratorException.cs src\CustomAllocator.cs src\CustomSerializable.cs src\CustomSerializer.cs src\Database.cs src\Enumerable.cs src\Indexable.cs src\FieldIndex.cs src\GenericIndex.cs src\IBidirectionalEnumerator.cs src\IFile.cs src\IFileStream.cs  src\Index.cs src\IPersistent.cs src\IPersistentList.cs src\IPersistentMap.cs src\IResource.cs src\ISet.cs src\ITable.cs src\JSQLArithmeticException.cs src\JSQLNoSuchFieldException.cs src\JSQLNullPointerException.cs src\JSQLRuntimeException.cs src\Key.cs src\L2List.cs src\L2ListElem.cs src\Link.cs src\MemoryUsage.cs src\NullFile.cs src\PArray.cs src\PatriciaTrie.cs src\PatriciaTrieKey.cs src\Persistent.cs src\PersistentCollection.cs src\PersistentComparator.cs src\PersistentEnumerator.cs src\PersistentResource.cs src\PersistentString.cs src\Projection.cs src\Query.cs src\CodeGenerator.cs src\Rectangle.cs src\RectangleR2.cs src\Relation.cs src\Resolver.cs src\SortedCollection.cs src\SpatialIndex.cs src\SpatialIndexR2.cs src\SpatialIndexRn.cs src\impl\RtreeRn.cs src\impl\RtreeRnPage.cs src\PointRn.cs src\RectangleRn.cs src\Sphere.cs src\Storage.cs src\StorageError.cs src\StorageFactory.cs src\StorageListener.cs src\QueryProfiler.cs src\StreamFile.cs src\TimeSeries.cs src\Version.cs src\VersionHistory.cs src\XMLImportException.cs src\impl\KDTree.cs src\impl\ReflectionMultidimensinalComparator.cs src\impl\AltBtree.cs src\impl\AltBtreeFieldIndex.cs src\impl\AltPersistentSet.cs src\impl\BitIndexImpl.cs src\impl\BitmapCustomAllocator.cs src\impl\BlobImpl.cs src\impl\Btree.cs src\impl\BtreeCompoundIndex.cs src\impl\BtreeFieldIndex.cs src\impl\BtreeKey.cs src\impl\BtreeMultiFieldIndex.cs src\impl\BtreePage.cs src\impl\ByteBuffer.cs src\impl\Bytes.cs src\impl\ClassDescriptor.cs src\impl\CNetMonitor.cs src\impl\DefaultAllocator.cs src\impl\DefaultPersistentComparator.cs src\impl\GeneratedSerializer.cs src\impl\LinkImpl.cs src\impl\LRU.cs src\impl\LruObjectCache.cs src\impl\MultiFile.cs src\impl\ObjectHeader.cs src\impl\OidHashTable.cs src\impl\OSFile.cs src\impl\Page.cs src\impl\PagePool.cs src\impl\PArrayImpl.cs src\impl\PersistentListImpl.cs src\impl\PersistentMapImpl.cs src\impl\PersistentHashImpl.cs src\impl\PersistentSet.cs src\impl\PersistentStub.cs src\impl\PinWeakHashTable.cs src\impl\PTrie.cs src\impl\QueryImpl.cs src\impl\CodeGeneratorImpl.cs src\impl\Rc4File.cs src\impl\RelationImpl.cs src\impl\RndBtree.cs src\impl\RndBtreeFieldIndex.cs src\impl\Rtree.cs src\impl\RtreePage.cs src\impl\RtreeR2.cs src\impl\RtreeR2Page.cs src\impl\ScalableList.cs src\impl\ScalableSet.cs src\impl\StorageImpl.cs src\impl\StrongHashTable.cs src\impl\ThickIndex.cs src\impl\ThickFieldIndex.cs src\impl\TimeSeriesImpl.cs src\impl\Ttree.cs src\impl\TtreePage.cs src\impl\WeakHashTable.cs src\impl\XMLExporter.cs src\impl\XMLImporter.cs src\fulltext\FullTextIndex.cs src\fulltext\FullTextQuery.cs src\fulltext\FullTextSearchHelper.cs src\fulltext\FullTextSearchResult.cs src\fulltext\FullTextSearchable.cs src\fulltext\FullTextIndexable.cs src\fulltext\Occurrence.cs src\impl\FullTextIndexImpl.cs src\impl\ObjectMap.cs src\RegexIndex.cs src\impl\RegexIndexImpl.cs src\Bitmap.cs

!ELSEIF "$(TARGET)" == "COMPACT_NET_3.5" 

EXAMPLES=tst/Simple/bin/debug/Simple.exe tst/TestIndex/bin/debug/TestIndex.exe tst/TestPerf/bin/debug/TestPerf.exe tst/TestProp/bin/debug/TestProp.exe tst/TestKDTree/bin/debug/TestKDTree.exe tst/TestKDTree2/bin/debug/TestKDTree2.exe tst/TestMap/bin/debug/TestMap.exe tst/TestList2/bin/debug/TestList2.exe tst/TestRndIndex/bin/debug/TestRndIndex.exe tst/Guess/bin/debug/Guess.exe tst/TestXML/bin/debug/TestXML.exe tst/TestLink/bin/debug/TestLink.exe tst/TestConcur/bin/debug/TestConcur.exe tst/TestServer/bin/debug/TestServer.exe tst/TestGC/bin/debug/TestGC.exe tst/TestRtree/bin/debug/TestRtree.exe tst/TestSSD/bin/debug/TestSSD.exe tst/TestCompoundIndex/bin/debug/TestCompoundIndex.exe tst/TestEnumerator/bin/debug/TestEnumerator.exe tst/TestTtree/bin/debug/TestTtree.exe tst/TestBlob/bin/debug/TestBlob.exe tst/TestAlloc/bin/debug/TestAlloc.exe tst/TestLeak/bin/debug/TestLeak.exe tst/TestBackup/bin/debug/TestBackup.exe tst/TestTimeSeries/bin/debug/TestTimeSeries.exe tst/TestSOD/bin/debug/TestSOD.exe tst/TestIndex2/bin/debug/TestIndex2.exe tst/TestR2/bin/debug/TestR2.exe tst/TestRaw/bin/debug/TestRaw.exe tst/IpCountry/bin/debug/IpCountry.exe tst/TestBit/bin/debug/TestBit.exe tst/TestBitmap/bin/debug/TestBitmap.exe tst/TestJSQL/bin/debug/TestJSQL.exe tst/TestIndexCE/bin/debug/TestIndexCE.exe tst/TestFullTextIndex/bin/debug/TestFullTextIndex.exe tst/SearchEngine/bin/debug/SearchEngine.exe tst/TestLockUpgrade/bin/debug/TestLockUpgrade.exe tst/TestRegex/bin/debug/TestRegex.exe

SOURCES=src\AssemblyInfo.cs src\ISelfSerializable.cs src\impl\MemoryReader.cs src\MultidimensionalIndex.cs src\MultidimensionalComparator.cs src\BitIndex.cs src\Blob.cs src\ClassLoader.cs src\CompileError.cs src\CodeGeneratorException.cs src\CustomAllocator.cs src\CustomSerializable.cs src\CustomSerializer.cs src\Database.cs src\Enumerable.cs src\Indexable.cs src\FieldIndex.cs src\GenericIndex.cs src\IBidirectionalEnumerator.cs src\IFile.cs src\IFileStream.cs  src\Index.cs src\IPersistent.cs src\IPersistentList.cs src\IPersistentMap.cs src\IResource.cs src\ISet.cs src\ITable.cs src\JSQLArithmeticException.cs src\JSQLNoSuchFieldException.cs src\JSQLNullPointerException.cs src\JSQLRuntimeException.cs src\Key.cs src\L2List.cs src\L2ListElem.cs src\Link.cs src\MemoryUsage.cs src\NullFile.cs src\CompressedFile.cs src\PArray.cs src\PatriciaTrie.cs src\PatriciaTrieKey.cs src\Persistent.cs src\PersistentCollection.cs src\PersistentComparator.cs src\PersistentEnumerator.cs src\PersistentResource.cs src\PersistentString.cs src\Projection.cs src\Query.cs src\CodeGenerator.cs src\Rectangle.cs src\RectangleR2.cs src\Relation.cs src\Resolver.cs src\SortedCollection.cs src\SpatialIndex.cs src\SpatialIndexR2.cs src\SpatialIndexRn.cs src\impl\RtreeRn.cs src\impl\RtreeRnPage.cs src\PointRn.cs src\RectangleRn.cs src\Sphere.cs src\Storage.cs src\StorageError.cs src\StorageFactory.cs src\StorageListener.cs src\QueryProfiler.cs src\StreamFile.cs src\TimeSeries.cs src\Version.cs src\VersionHistory.cs src\XMLImportException.cs src\impl\KDTree.cs src\impl\ReflectionMultidimensinalComparator.cs src\impl\AltBtree.cs src\impl\AltBtreeFieldIndex.cs src\impl\AltPersistentSet.cs src\impl\BitIndexImpl.cs src\impl\BitmapCustomAllocator.cs src\impl\BlobImpl.cs src\impl\Btree.cs src\impl\BtreeCompoundIndex.cs src\impl\BtreeFieldIndex.cs src\impl\BtreeKey.cs src\impl\BtreeMultiFieldIndex.cs src\impl\BtreePage.cs src\impl\ByteBuffer.cs src\impl\Bytes.cs src\impl\ClassDescriptor.cs src\impl\CNetMonitor.cs src\impl\DefaultAllocator.cs src\impl\DefaultPersistentComparator.cs src\impl\GeneratedSerializer.cs src\impl\LinkImpl.cs src\impl\LRU.cs src\impl\LruObjectCache.cs src\impl\MultiFile.cs src\impl\ObjectHeader.cs src\impl\OidHashTable.cs src\impl\OSFile.cs src\impl\Page.cs src\impl\PagePool.cs src\impl\PArrayImpl.cs src\impl\PersistentListImpl.cs src\impl\PersistentMapImpl.cs src\impl\PersistentHashImpl.cs src\impl\PersistentSet.cs src\impl\PersistentStub.cs src\impl\PinWeakHashTable.cs src\impl\PTrie.cs src\impl\QueryImpl.cs src\impl\CodeGeneratorImpl.cs src\impl\Rc4File.cs src\impl\RelationImpl.cs src\impl\RndBtree.cs src\impl\RndBtreeFieldIndex.cs src\impl\Rtree.cs src\impl\RtreePage.cs src\impl\RtreeR2.cs src\impl\RtreeR2Page.cs src\impl\ScalableList.cs src\impl\ScalableSet.cs src\impl\StorageImpl.cs src\impl\StrongHashTable.cs src\impl\ThickIndex.cs src\impl\ThickFieldIndex.cs src\impl\TimeSeriesImpl.cs src\impl\Ttree.cs src\impl\TtreePage.cs src\impl\WeakHashTable.cs src\impl\XMLExporter.cs src\impl\XMLImporter.cs src\fulltext\FullTextIndex.cs src\fulltext\FullTextQuery.cs src\fulltext\FullTextSearchHelper.cs src\fulltext\FullTextSearchResult.cs src\fulltext\FullTextSearchable.cs src\fulltext\FullTextIndexable.cs src\fulltext\Occurrence.cs src\impl\FullTextIndexImpl.cs src\impl\ObjectMap.cs src\SmallDictionary.cs src\RegexIndex.cs src\impl\RegexIndexImpl.cs src\Bitmap.cs

!ELSEIF "$(TARGET)" == "SILVERLIGHT" || "$(TARGET)" == "SILVERLIGHT_GENERICS" 

EXAMPLES=

SOURCES=src\BitIndex.cs src\Blob.cs src\ClassLoader.cs src\CodeGenerator.cs src\CodeGeneratorException.cs src\CompileError.cs src\CustomAllocator.cs src\CustomSerializable.cs src\CustomSerializer.cs src\Database.cs src\Enumerable.cs src\FieldIndex.cs src\fulltext\FullTextIndex.cs src\fulltext\FullTextIndexable.cs src\fulltext\FullTextQuery.cs src\fulltext\FullTextSearchable.cs src\fulltext\FullTextSearchHelper.cs src\fulltext\FullTextSearchResult.cs src\fulltext\Occurrence.cs src\GenericIndex.cs src\IBidirectionalEnumerator.cs src\IFile.cs src\IFileStream.cs src\impl\AltBtree.cs src\impl\AltBtreeCompoundIndex.cs src\impl\AltBtreeFieldIndex.cs src\impl\AltBtreeMultiFieldIndex.cs src\impl\AltPersistentSet.cs src\impl\BitIndexImpl.cs src\impl\BitmapCustomAllocator.cs src\impl\BlobImpl.cs src\impl\Btree.cs src\impl\BtreeCompoundIndex.cs src\impl\BtreeFieldIndex.cs src\impl\BtreeKey.cs src\impl\BtreeMultiFieldIndex.cs src\impl\BtreePage.cs src\impl\ByteBuffer.cs src\impl\Bytes.cs src\impl\CILGenerator.cs src\impl\ClassDescriptor.cs src\impl\CNetMonitor.cs src\impl\CodeGeneratorImpl.cs src\impl\DefaultAllocator.cs src\impl\DefaultPersistentComparator.cs src\impl\FullTextIndexImpl.cs src\impl\GeneratedSerializer.cs src\impl\KDTree.cs src\impl\LinkImpl.cs src\impl\LRU.cs src\impl\LruObjectCache.cs src\impl\MemoryReader.cs src\impl\MultiFile.cs src\impl\ObjectHeader.cs src\impl\ObjectMap.cs src\impl\OidHashTable.cs src\impl\OSFile.cs src\impl\Page.cs src\impl\PagePool.cs src\impl\PArrayImpl.cs src\impl\PersistentHashImpl.cs src\impl\PersistentListImpl.cs src\impl\PersistentMapImpl.cs src\impl\PersistentSet.cs src\impl\PersistentStub.cs src\impl\PersistentWrapper.cs src\impl\PinWeakHashTable.cs src\impl\PTrie.cs src\impl\QueryImpl.cs src\impl\Rc4File.cs src\impl\ReflectionMultidimensinalComparator.cs src\impl\RelationImpl.cs src\impl\RndBtree.cs src\impl\RndBtreeCompoundIndex.cs src\impl\RndBtreeFieldIndex.cs src\impl\RndBtreeMultiFieldIndex.cs src\impl\Rtree.cs src\impl\RtreePage.cs src\impl\RtreeR2.cs src\impl\RtreeR2Page.cs src\impl\ScalableList.cs src\impl\ScalableSet.cs src\impl\StorageImpl.cs src\impl\StrongHashTable.cs src\impl\ThickIndex.cs src\impl\ThickFieldIndex.cs src\impl\TimeSeriesImpl.cs src\impl\Ttree.cs src\impl\TtreePage.cs src\impl\WeakHashTable.cs src\impl\XMLExporter.cs src\impl\XMLImporter.cs src\Index.cs src\Indexable.cs src\IPersistent.cs src\IPersistentList.cs src\IPersistentMap.cs src\IResource.cs src\ISelfSerializable.cs src\ISet.cs src\ITable.cs src\JSQLArithmeticException.cs src\JSQLNoSuchFieldException.cs src\JSQLNullPointerException.cs src\JSQLRuntimeException.cs src\Key.cs src\L2List.cs src\L2ListElem.cs src\Link.cs src\MemoryUsage.cs src\MultidimensionalComparator.cs src\MultidimensionalIndex.cs src\NullFile.cs src\PArray.cs src\PatriciaTrie.cs src\PatriciaTrieKey.cs src\Persistent.cs src\PersistentCollection.cs src\PersistentComparator.cs src\PersistentEnumerator.cs src\PersistentResource.cs src\PersistentString.cs src\Projection.cs src\Properties\AssemblyInfo.cs src\Query.cs src\Rectangle.cs src\RectangleR2.cs src\Relation.cs src\Resolver.cs src\SilverlightStubs.cs src\SortedCollection.cs src\SpatialIndex.cs src\SpatialIndexR2.cs src\SpatialIndexRn.cs src\impl\RtreeRn.cs src\impl\RtreeRnPage.cs src\PointRn.cs src\RectangleRn.cs src\Sphere.cs src\Storage.cs src\StorageError.cs src\StorageFactory.cs src\StorageListener.cs src\QueryProfiler.cs src\StreamFile.cs src\TimeSeries.cs src\Version.cs src\VersionHistory.cs src\XMLImportException.cs src\SmallDictionary.cs src\RegexIndex.cs src\impl\RegexIndexImpl.cs src\Bitmap.cs


!ELSEIF "$(TARGET)" == "NET_3.5" 

EXAMPLES=tst/Simple/bin/debug/Simple.exe tst/IpCountry/bin/debug/IpCountry.exe tst/Guess/bin/debug/Guess.exe tst/PropGuess/bin/debug/Guess.exe tst/TestBackup/bin/debug/TestBackup.exe tst/TestBit/bin/debug/TestBit.exe tst/TestBitmap/bin/debug/TestBitmap.exe tst/TestBlob/bin/debug/TestBlob.exe tst/TestAlloc/bin/debug/TestAlloc.exe tst/TestLeak/bin/debug/TestLeak.exe tst/TestCompoundIndex/bin/debug/TestCompoundIndex.exe tst/TestConcur/bin/debug/TestConcur.exe tst/TestServer/bin/debug/TestServer.exe tst/TestEnumerator/bin/debug/TestEnumerator.exe tst/TestGC/bin/debug/TestGC.exe tst/TestIndex/bin/debug/TestIndex.exe tst/TestPerf/bin/debug/TestPerf.exe tst/TestProp/bin/debug/TestProp.exe tst/TestKDTree/bin/debug/TestKDTree.exe tst/TestKDTree2/bin/debug/TestKDTree2.exe tst/TestMap/bin/debug/TestMap.exe tst/TestIndex2/bin/debug/TestIndex2.exe tst/TestLink/bin/debug/TestLink.exe tst/TestList/bin/debug/TestList.exe  tst/TestList2/bin/debug/TestList2.exe tst/TestR2/bin/debug/TestR2.exe tst/TestRaw/bin/debug/TestRaw.exe tst/TestRtree/bin/debug/TestRtree.exe tst/TestSOD/bin/debug/TestSOD.exe tst/TestSSD/bin/debug/TestSSD.exe tst/TestTimeSeries/bin/debug/TestTimeSeries.exe tst/TestTtree/bin/debug/TestTtree.exe tst/TestXML/bin/debug/TestXML.exe tst/TransparentGuess/bin/debug/Guess.exe tst/TestReplic/bin/debug/TestReplic.exe tst/TestReplic2/bin/debug/TestReplic2.exe tst/TestJSQL/bin/debug/TestJSQL.exe tst/TestRndIndex/bin/debug/TestRndIndex.exe tst/TestNullable/bin/debug/TestNullable.exe tst/TestLinq/bin/debug/TestLinq.exe tst/TestFullTextIndex/bin/debug/TestFullTextIndex.exe tst/SearchEngine/bin/debug/SearchEngine.exe tst/TestLockUpgrade/bin/debug/TestLockUpgrade.exe tst/TestRegex/bin/debug/TestRegex.exe

SOURCES=src\\*.cs src\\impl\\*.cs src\\fulltext\\*.cs

!ELSE

EXAMPLES=tst/Simple/bin/debug/Simple.exe tst/IpCountry/bin/debug/IpCountry.exe tst/Guess/bin/debug/Guess.exe tst/PropGuess/bin/debug/Guess.exe tst/TestBackup/bin/debug/TestBackup.exe tst/TestBit/bin/debug/TestBit.exe tst/TestBitmap/bin/debug/TestBitmap.exe tst/TestBlob/bin/debug/TestBlob.exe tst/TestAlloc/bin/debug/TestAlloc.exe tst/TestLeak/bin/debug/TestLeak.exe tst/TestCompoundIndex/bin/debug/TestCompoundIndex.exe tst/TestConcur/bin/debug/TestConcur.exe tst/TestServer/bin/debug/TestServer.exe tst/TestEnumerator/bin/debug/TestEnumerator.exe tst/TestGC/bin/debug/TestGC.exe tst/TestIndex/bin/debug/TestIndex.exe tst/TestPerf/bin/debug/TestPerf.exe tst/TestProp/bin/debug/TestProp.exe tst/TestKDTree/bin/debug/TestKDTree.exe tst/TestKDTree2/bin/debug/TestKDTree2.exe tst/TestMap/bin/debug/TestMap.exe tst/TestIndex2/bin/debug/TestIndex2.exe tst/TestLink/bin/debug/TestLink.exe tst/TestList/bin/debug/TestList.exe  tst/TestList2/bin/debug/TestList2.exe tst/TestR2/bin/debug/TestR2.exe tst/TestRaw/bin/debug/TestRaw.exe tst/TestRtree/bin/debug/TestRtree.exe tst/TestSOD/bin/debug/TestSOD.exe tst/TestSSD/bin/debug/TestSSD.exe tst/TestTimeSeries/bin/debug/TestTimeSeries.exe tst/TestTtree/bin/debug/TestTtree.exe tst/TestXML/bin/debug/TestXML.exe tst/TransparentGuess/bin/debug/Guess.exe tst/TestReplic/bin/debug/TestReplic.exe tst/TestReplic2/bin/debug/TestReplic2.exe tst/TestJSQL/bin/debug/TestJSQL.exe tst/TestRndIndex/bin/debug/TestRndIndex.exe tst/TestNullable/bin/debug/TestNullable.exe tst/TestFullTextIndex/bin/debug/TestFullTextIndex.exe tst/SearchEngine/bin/debug/SearchEngine.exe tst/TestLockUpgrade/bin/debug/TestLockUpgrade.exe tst/TestRegex/bin/debug/TestRegex.exe

SOURCES=src\\*.cs src\\impl\\*.cs src\\fulltext\\*.cs

!ENDIF

!IF "$(TARGET)" != "GENERICS" && "$(TARGET)" != "SILVERLIGHT_GENERICS" && "$(TARGET)" != "SILVERLIGHT"
EXAMPLES=$(EXAMPLES) tst/TestDbServer/bin/debug/TestDbServer.exe tst/TestJsqlJoin/bin/debug/TestJsqlJoin.exe tst/TestCodeGenerator/bin/debug/TestCodeGenerator.exe  tst/TestAutoIndices/bin/debug/TestAutoIndices.exe tst/JsqlSSD/bin/debug/JsqlSSD.exe 
!ENDIF


all: library examples

library: $(LIBRARY)

documentation: library
	md working
	cd working
	copy ..\doc\perst.xml comments.xml
	MRefBuilder ../bin/PerstNet20.dll /out:reflection.org
	XslTransform /xsl:"$(DXROOT)ProductionTransforms\ApplyVSDocModel.xsl" reflection.org /xsl:"$(DXROOT)ProductionTransforms\AddFriendlyFilenames.xsl" /out:reflection.xml
	XslTransform /xsl:"$(DXROOT)ProductionTransforms\ReflectionToManifest.xsl" reflection.xml /out:manifest.xml
	call "$(DXROOT)Presentation\vs2005\copyOutput.bat"
	BuildAssembler /config:..\sandcastle.config manifest.xml
	XslTransform /xsl:"$(DXROOT)ProductionTransforms\ReflectionToChmProject.xsl" reflection.xml /out:Output\test.hhp
	XslTransform /xsl:"$(DXROOT)ProductionTransforms\createvstoc.xsl" reflection.xml /out:toc.xml 
	XslTransform /xsl:"$(DXROOT)ProductionTransforms\TocToChmContents.xsl" toc.xml /out:Output\test.hhc
	XslTransform /xsl:"$(DXROOT)ProductionTransforms\ReflectionToChmIndex.xsl" reflection.xml /out:Output\test.hhk
	-"C:\Program Files\HTML Help Workshop\hhc.exe" Output\test.hhp 
	move /y Output\test.chm ..\doc\perst.chm
	cd ..
	rd /s/q working


$(LIBRARY): $(SOURCES)
	-md bin
	$(COMPILER) /target:library /out:$(LIBRARY) $(GENDOC) $(SOURCES)

examples: $(EXAMPLES)

tst/IpCountry/bin/debug/IpCountry.exe: tst/IpCountry/IpCountry.cs $(LIBRARY)
	-md tst\IpCountry\bin\debug
	copy $(LIBRARY) tst\IpCountry\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/IpCountry/bin/debug/IpCountry.exe tst\\IpCountry\\IpCountry.cs

tst/Guess/bin/debug/Guess.exe: tst/Guess/Guess.cs $(LIBRARY)
	-md tst\Guess\bin\debug
	copy $(LIBRARY) tst\Guess\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/Guess/bin/debug/Guess.exe tst\\Guess\\Guess.cs

tst/PropGuess/bin/debug/Guess.exe: tst/PropGuess/Guess.cs $(LIBRARY)
	-md tst\PropGuess\bin\debug
	copy $(LIBRARY) tst\PropGuess\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/PropGuess/bin/debug/Guess.exe tst\\PropGuess\\Guess.cs

tst/TransparentGuess/bin/debug/Guess.exe: tst/TransparentGuess/Guess.cs $(LIBRARY)
	-md tst\TransparentGuess\bin\debug
	copy $(LIBRARY) tst\TransparentGuess\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TransparentGuess/bin/debug/Guess.exe tst\\TransparentGuess\\Guess.cs

tst/TestBackup/bin/debug/TestBackup.exe: tst/TestBackup/TestBackup.cs $(LIBRARY)
	-md tst\TestBackup\bin\debug
	copy $(LIBRARY) tst\TestBackup\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestBackup/bin/debug/TestBackup.exe tst\\TestBackup\\TestBackup.cs

tst/TestBit/bin/debug/TestBit.exe: tst/TestBit/TestBit.cs $(LIBRARY)
	-md tst\TestBit\bin\debug
	copy $(LIBRARY) tst\TestBit\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestBit/bin/debug/TestBit.exe tst\\TestBit\\TestBit.cs

tst/TestBitmap/bin/debug/TestBitmap.exe: tst/TestBitmap/TestBitmap.cs $(LIBRARY)
	-md tst\TestBitmap\bin\debug
	copy $(LIBRARY) tst\TestBitmap\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestBitmap/bin/debug/TestBitmap.exe tst\\TestBitmap\\TestBitmap.cs

tst/TestBlob/bin/debug/TestBlob.exe: tst/TestBlob/TestBlob.cs $(LIBRARY)
	-md tst\TestBlob\bin\debug
	copy $(LIBRARY) tst\TestBlob\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestBlob/bin/debug/TestBlob.exe tst\\TestBlob\\TestBlob.cs

tst/TestFullTextIndex/bin/debug/TestFullTextIndex.exe: tst/TestFullTextIndex/TestFullTextIndex.cs $(LIBRARY)
	-md tst\TestFullTextIndex\bin\debug
	copy $(LIBRARY) tst\TestFullTextIndex\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestFullTextIndex/bin/debug/TestFullTextIndex.exe tst\\TestFullTextIndex\\TestFullTextIndex.cs

tst/SearchEngine/bin/debug/SearchEngine.exe: tst/SearchEngine/SearchEngine.cs $(LIBRARY)
	-md tst\SearchEngine\bin\debug
	copy $(LIBRARY) tst\SearchEngine\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/SearchEngine/bin/debug/SearchEngine.exe tst\\SearchEngine\\SearchEngine.cs

tst/TestAlloc/bin/debug/TestAlloc.exe: tst/TestAlloc/TestAlloc.cs $(LIBRARY)
	-md tst\TestAlloc\bin\debug
	copy $(LIBRARY) tst\TestAlloc\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestAlloc/bin/debug/TestAlloc.exe tst\\TestAlloc\\TestAlloc.cs

tst/TestLeak/bin/debug/TestLeak.exe: tst/TestLeak/TestLeak.cs $(LIBRARY)
	-md tst\TestLeak\bin\debug
	copy $(LIBRARY) tst\TestLeak\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestLeak/bin/debug/TestLeak.exe tst\\TestLeak\\TestLeak.cs

tst/TestCompoundIndex/bin/debug/TestCompoundIndex.exe: tst/TestCompoundIndex/TestCompoundIndex.cs $(LIBRARY)
	-md tst\TestCompoundIndex\bin\debug
	copy $(LIBRARY) tst\TestCompoundIndex\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestCompoundIndex/bin/debug/TestCompoundIndex.exe tst\\TestCompoundIndex\\TestCompoundIndex.cs

tst/TestConcur/bin/debug/TestConcur.exe: tst/TestConcur/TestConcur.cs $(LIBRARY)
	-md tst\TestConcur\bin\debug
	copy $(LIBRARY) tst\TestConcur\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestConcur/bin/debug/TestConcur.exe tst\\TestConcur\\TestConcur.cs

tst/TestLockUpgrade/bin/debug/TestLockUpgrade.exe: tst/TestLockUpgrade/TestLockUpgrade.cs $(LIBRARY)
	-md tst\TestLockUpgrade\bin\debug
	copy $(LIBRARY) tst\TestLockUpgrade\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestLockUpgrade/bin/debug/TestLockUpgrade.exe tst\\TestLockUpgrade\\TestLockUpgrade.cs

tst/TestServer/bin/debug/TestServer.exe: tst/TestServer/TestServer.cs $(LIBRARY)
	-md tst\TestServer\bin\debug
	copy $(LIBRARY) tst\TestServer\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestServer/bin/debug/TestServer.exe tst\\TestServer\\TestServer.cs

tst/TestDbServer/bin/debug/TestDbServer.exe: tst/TestDbServer/TestDbServer.cs $(LIBRARY)
	-md tst\TestDbServer\bin\debug
	copy $(LIBRARY) tst\TestDbServer\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestDbServer/bin/debug/TestDbServer.exe tst\\TestDbServer\\TestDbServer.cs

tst/TestEnumerator/bin/debug/TestEnumerator.exe: tst/TestEnumerator/TestEnumerator.cs $(LIBRARY)
	-md tst\TestEnumerator\bin\debug
	copy $(LIBRARY) tst\TestEnumerator\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestEnumerator/bin/debug/TestEnumerator.exe tst\\TestEnumerator\\TestEnumerator.cs

tst/TestGC/bin/debug/TestGC.exe: tst/TestGC/TestGC.cs $(LIBRARY)
	-md tst\TestGC\bin\debug
	copy $(LIBRARY) tst\TestGC\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestGC/bin/debug/TestGC.exe tst\\TestGC\\TestGC.cs

tst/Simple/bin/debug/Simple.exe: tst/Simple/Simple.cs $(LIBRARY)
	-md tst\Simple\bin\debug
	copy $(LIBRARY) tst\Simple\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/Simple/bin/debug/Simple.exe tst\\Simple\\Simple.cs

tst/TestIndex/bin/debug/TestIndex.exe: tst/TestIndex/TestIndex.cs $(LIBRARY)
	-md tst\TestIndex\bin\debug
	copy $(LIBRARY) tst\TestIndex\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestIndex/bin/debug/TestIndex.exe tst\\TestIndex\\TestIndex.cs

tst/TestRegex/bin/debug/TestRegex.exe: tst/TestRegex/TestRegex.cs $(LIBRARY)
	-md tst\TestRegex\bin\debug
	copy $(LIBRARY) tst\TestRegex\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestRegex/bin/debug/TestRegex.exe tst\\TestRegex\\TestRegex.cs

tst/TestPerf/bin/debug/TestPerf.exe: tst/TestPerf/TestPerf.cs $(LIBRARY)
	-md tst\TestPerf\bin\debug
	copy $(LIBRARY) tst\TestPerf\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestPerf/bin/debug/TestPerf.exe tst\\TestPerf\\TestPerf.cs

tst/TestProp/bin/debug/TestProp.exe: tst/TestProp/TestProp.cs $(LIBRARY)
	-md tst\TestProp\bin\debug
	copy $(LIBRARY) tst\TestProp\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestProp/bin/debug/TestProp.exe tst\\TestProp\\TestProp.cs

tst/TestIndexCE/bin/debug/TestIndexCE.exe: tst/TestIndexCE/TestIndexCE.cs $(LIBRARY)
	-md tst\TestIndexCE\bin\debug
	copy $(LIBRARY) tst\TestIndexCE\bin\debug
	$(COMPILER) /target:winexe /r:$(LIBRARY) /r:"$(WINCE_CF20_HOME)\\System.Drawing.dll" /r:"$(WINCE_CF20_HOME)\\System.Windows.Forms.dll" /out:tst/TestIndexCE/bin/debug/TestIndexCE.exe tst\\TestIndexCE\\TestIndexCE.cs

tst/TestNullable/bin/debug/TestNullable.exe: tst/TestNullable/TestNullable.cs $(LIBRARY)
	-md tst\TestNullable\bin\debug
	copy $(LIBRARY) tst\TestNullable\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestNullable/bin/debug/TestNullable.exe tst\\TestNullable\\TestNullable.cs

tst/TestKDTree/bin/debug/TestKDTree.exe: tst/TestKDTree/TestKDTree.cs $(LIBRARY)
	-md tst\TestKDTree\bin\debug
	copy $(LIBRARY) tst\TestKDTree\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestKDTree/bin/debug/TestKDTree.exe tst\\TestKDTree\\TestKDTree.cs

tst/TestKDTree2/bin/debug/TestKDTree2.exe: tst/TestKDTree2/TestKDTree2.cs $(LIBRARY)
	-md tst\TestKDTree2\bin\debug
	copy $(LIBRARY) tst\TestKDTree2\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestKDTree2/bin/debug/TestKDTree2.exe tst\\TestKDTree2\\TestKDTree2.cs

tst/TestMap/bin/debug/TestMap.exe: tst/TestMap/TestMap.cs $(LIBRARY)
	-md tst\TestMap\bin\debug
	copy $(LIBRARY) tst\TestMap\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestMap/bin/debug/TestMap.exe tst\\TestMap\\TestMap.cs

tst/TestIndex2/bin/debug/TestIndex2.exe: tst/TestIndex2/TestIndex2.cs $(LIBRARY)
	-md tst\TestIndex2\bin\debug
	copy $(LIBRARY) tst\TestIndex2\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestIndex2/bin/debug/TestIndex2.exe tst\\TestIndex2\\TestIndex2.cs

tst/TestLink/bin/debug/TestLink.exe: tst/TestLink/TestLink.cs $(LIBRARY)
	-md tst\TestLink\bin\debug
	copy $(LIBRARY) tst\TestLink\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestLink/bin/debug/TestLink.exe tst\\TestLink\\TestLink.cs

tst/TestLinq/bin/debug/TestLinq.exe: tst/TestLinq/TestLinq.cs $(LIBRARY)
	-md tst\TestLinq\bin\debug
	copy $(LIBRARY) tst\TestLinq\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestLinq/bin/debug/TestLinq.exe tst\\TestLinq\\TestLinq.cs

tst/TestList2/bin/debug/TestList2.exe: tst/TestList2/TestList2.cs $(LIBRARY)
	-md tst\TestList2\bin\debug
	copy $(LIBRARY) tst\TestList2\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestList2/bin/debug/TestList2.exe tst\\TestList2\\TestList2.cs

tst/TestRndIndex/bin/debug/TestRndIndex.exe: tst/TestRndIndex/TestRndIndex.cs $(LIBRARY)
	-md tst\TestRndIndex\bin\debug
	copy $(LIBRARY) tst\TestRndIndex\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestRndIndex/bin/debug/TestRndIndex.exe tst\\TestRndIndex\\TestRndIndex.cs

tst/TestList/bin/debug/TestList.exe: tst/TestList/TestList.cs $(LIBRARY)
	-md tst\TestList\bin\debug
	copy $(LIBRARY) tst\TestList\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestList/bin/debug/TestList.exe tst\\TestList\\TestList.cs

tst/TestR2/bin/debug/TestR2.exe: tst/TestR2/TestR2.cs $(LIBRARY)
	-md tst\TestR2\bin\debug
	copy $(LIBRARY) tst\TestR2\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestR2/bin/debug/TestR2.exe tst\\TestR2\\TestR2.cs

tst/TestRaw/bin/debug/TestRaw.exe: tst/TestRaw/TestRaw.cs $(LIBRARY)
	-md tst\TestRaw\bin\debug
	copy $(LIBRARY) tst\TestRaw\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestRaw/bin/debug/TestRaw.exe tst\\TestRaw\\TestRaw.cs

tst/TestRtree/bin/debug/TestRtree.exe: tst/TestRtree/TestRtree.cs $(LIBRARY)
	-md tst\TestRtree\bin\debug
	copy $(LIBRARY) tst\TestRtree\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestRtree/bin/debug/TestRtree.exe tst\\TestRtree\\TestRtree.cs

tst/TestSOD/bin/debug/TestSOD.exe: tst/TestSOD/TestSOD.cs $(LIBRARY)
	-md tst\TestSOD\bin\debug
	copy $(LIBRARY) tst\TestSOD\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestSOD/bin/debug/TestSOD.exe tst\\TestSOD\\TestSOD.cs

tst/TestSSD/bin/debug/TestSSD.exe: tst/TestSSD/TestSSD.cs $(LIBRARY)
	-md tst\TestSSD\bin\debug
	copy $(LIBRARY) tst\TestSSD\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestSSD/bin/debug/TestSSD.exe tst\\TestSSD\\TestSSD.cs

tst/JsqlSSD/bin/debug/JsqlSSD.exe: tst/JsqlSSD/JsqlSSD.cs $(LIBRARY)
	-md tst\JsqlSSD\bin\debug
	copy $(LIBRARY) tst\JsqlSSD\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/JsqlSSD/bin/debug/JsqlSSD.exe tst\\JsqlSSD\\JsqlSSD.cs

tst/TestTimeSeries/bin/debug/TestTimeSeries.exe: tst/TestTimeSeries/TestTimeSeries.cs $(LIBRARY)
	-md tst\TestTimeSeries\bin\debug
	copy $(LIBRARY) tst\TestTimeSeries\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestTimeSeries/bin/debug/TestTimeSeries.exe tst\\TestTimeSeries\\TestTimeSeries.cs

tst/TestTtree/bin/debug/TestTtree.exe: tst/TestTtree/TestTtree.cs $(LIBRARY)
	-md tst\TestTtree\bin\debug
	copy $(LIBRARY) tst\TestTtree\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestTtree/bin/debug/TestTtree.exe tst\\TestTtree\\TestTtree.cs

tst/TestXML/bin/debug/TestXML.exe: tst/TestXML/TestXML.cs $(LIBRARY)
	-md tst\TestXML\bin\debug
	copy $(LIBRARY) tst\TestXML\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestXML/bin/debug/TestXML.exe tst\\TestXML\\TestXML.cs

tst/TestReplic/bin/debug/TestReplic.exe: tst/TestReplic/TestReplic.cs $(LIBRARY)
	-md tst\TestReplic\bin\debug
	copy $(LIBRARY) tst\TestReplic\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestReplic/bin/debug/TestReplic.exe tst\\TestReplic\\TestReplic.cs

tst/TestReplic2/bin/debug/TestReplic2.exe: tst/TestReplic2/TestReplic2.cs $(LIBRARY)
	-md tst\TestReplic2\bin\debug
	copy $(LIBRARY) tst\TestReplic2\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestReplic2/bin/debug/TestReplic2.exe tst\\TestReplic2\\TestReplic2.cs

tst/TestJSQL/bin/debug/TestJSQL.exe: tst/TestJSQL/TestJSQL.cs $(LIBRARY)
	-md tst\TestJSQL\bin\debug
	copy $(LIBRARY) tst\TestJSQL\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestJSQL/bin/debug/TestJSQL.exe tst\\TestJSQL\\TestJSQL.cs

tst/TestJsqlJoin/bin/debug/TestJsqlJoin.exe: tst/TestJsqlJoin/TestJsqlJoin.cs $(LIBRARY)
	-md tst\TestJsqlJoin\bin\debug
	copy $(LIBRARY) tst\TestJsqlJoin\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestJsqlJoin/bin/debug/TestJsqlJoin.exe tst\\TestJsqlJoin\\TestJsqlJoin.cs

tst/TestCodeGenerator/bin/debug/TestCodeGenerator.exe: tst/TestCodeGenerator/TestCodeGenerator.cs $(LIBRARY)
	-md tst\TestCodeGenerator\bin\debug
	copy $(LIBRARY) tst\TestCodeGenerator\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestCodeGenerator/bin/debug/TestCodeGenerator.exe tst\\TestCodeGenerator\\TestCodeGenerator.cs

tst/TestAutoIndices/bin/debug/TestAutoIndices.exe: tst/TestAutoIndices/TestAutoIndices.cs $(LIBRARY)
	-md tst\TestAutoIndices\bin\debug
	copy $(LIBRARY) tst\TestAutoIndices\bin\debug
	$(COMPILER) /r:$(LIBRARY) /out:tst/TestAutoIndices/bin/debug/TestAutoIndices.exe tst\\TestAutoIndices\\TestAutoIndices.cs



clean:
	-rd /s/q obj
	-rd /s/q tst\Guess\obj
	-rd /s/q tst\Guess\bin
	-rd /s/q tst\TransparentGuess\obj
	-rd /s/q tst\TransparentGuess\bin
	-rd /s/q tst\PropGuess\obj
	-rd /s/q tst\PropGuess\bin
	-rd /s/q tst\TestIndex\obj
	-rd /s/q tst\TestIndex\bin
	-rd /s/q tst\TestRegex\obj
	-rd /s/q tst\TestRegex\bin
	-rd /s/q tst\TestPerf\obj
	-rd /s/q tst\TestPerf\bin
	-rd /s/q tst\TestProp\obj
	-rd /s/q tst\TestProp\bin
	-rd /s/q tst\Simple\obj
	-rd /s/q tst\Simple\bin
	-rd /s/q tst\TestKDTree\obj
	-rd /s/q tst\TestKDTree\bin
	-rd /s/q tst\TestKDTree2\obj
	-rd /s/q tst\TestKDTree2\bin
	-rd /s/q tst\TestMap\obj
	-rd /s/q tst\TestMap\bin
	-rd /s/q tst\TestIndex2\obj
	-rd /s/q tst\TestIndex2\bin
	-rd /s/q tst\TestLink\obj
	-rd /s/q tst\TestLink\bin
	-rd /s/q tst\TestLinq\obj
	-rd /s/q tst\TestLinq\bin
	-rd /s/q tst\TestList\obj
	-rd /s/q tst\TestList\bin
	-rd /s/q tst\TestList2\obj
	-rd /s/q tst\TestList2\bin
	-rd /s/q tst\TestRndIndex\obj
	-rd /s/q tst\TestRndIndex\bin
	-rd /s/q tst\TestGC\obj
	-rd /s/q tst\TestGC\bin
	-rd /s/q tst\TestConcur\obj
	-rd /s/q tst\TestConcur\bin
	-rd /s/q tst\TestLockUpgrade\obj
	-rd /s/q tst\TestLockUpgrade\bin
	-rd /s/q tst\TestServer\obj
	-rd /s/q tst\TestServer\bin
	-rd /s/q tst\TestDbServer\obj
	-rd /s/q tst\TestDbServer\bin
	-rd /s/q tst\TestRtree\obj
	-rd /s/q tst\TestRtree\bin
	-rd /s/q tst\TestR2\obj
	-rd /s/q tst\TestR2\bin
	-rd /s/q tst\TestTtree\obj
	-rd /s/q tst\TestTtree\bin
	-rd /s/q tst\TestXML\obj
	-rd /s/q tst\TestXML\bin
	-rd /s/q tst\TestBackup\obj
	-rd /s/q tst\TestBackup\bin
	-rd /s/q tst\TestRaw\obj
	-rd /s/q tst\TestRaw\bin
	-rd /s/q tst\TestSSD\bin
	-rd /s/q tst\TestSSD\obj
	-rd /s/q tst\JsqlSSD\bin
	-rd /s/q tst\JsqlSSD\obj
	-rd /s/q tst\TestSOD\bin
	-rd /s/q tst\TestSOD\obj
	-rd /s/q tst\TestEnumerator\obj
	-rd /s/q tst\TestEnumerator\bin
	-rd /s/q tst\TestCompoundIndex\obj
	-rd /s/q tst\TestCompoundIndex\bin
	-rd /s/q tst\TestBlob\obj
	-rd /s/q tst\TestBlob\bin
	-rd /s/q tst\TestFullTextIndex\obj
	-rd /s/q tst\TestFullTextIndex\bin
	-rd /s/q tst\SearchEngine\obj
	-rd /s/q tst\SearchEngine\bin
	-rd /s/q tst\TestAlloc\obj
	-rd /s/q tst\TestAlloc\bin
	-rd /s/q tst\TestLeak\obj
	-rd /s/q tst\TestLeak\bin
	-rd /s/q tst\TestNullable\obj
	-rd /s/q tst\TestNullable\bin
	-rd /s/q tst\TestBit\obj
	-rd /s/q tst\TestBit\bin
	-rd /s/q tst\TestBitmap\obj
	-rd /s/q tst\TestBitmap\bin
	-rd /s/q tst\TestReplic\obj
	-rd /s/q tst\TestReplic\bin
	-rd /s/q tst\TestReplic2\obj
	-rd /s/q tst\TestReplic2\bin
	-rd /s/q tst\TestTimeSeries\obj
	-rd /s/q tst\TestTimeSeries\bin
	-rd /s/q tst\IpCountry\obj
	-rd /s/q tst\IpCountry\bin
	-rd /s/q tst\TestJSQL\bin
	-rd /s/q tst\TestJSQL\obj
	-rd /s/q tst\TestJsqlJoin\bin
	-rd /s/q tst\TestJsqlJoin\obj
	-rd /s/q tst\TestCodeGenerator\bin
	-rd /s/q tst\TestCodeGenerator\obj
	-rd /s/q tst\TestAutoIndices\bin
	-rd /s/q tst\TestAutoIndices\obj
	-rd /s/q Rdf\bin
	-rd /s/q Rdf\obj
	-rd /s/q Rdf\XmlServer\bin
	-rd /s/q Rdf\XmlServer\obj
	-del /q Rdf\samples\*.dbs
	-del /q tst\IpCountry\*.dbs
	-del /q *.dbs
	-del /q *.xml
	-del /q *.suo
	-del /q *.ncb

zip: clean
	cd ..
	del /q perst4net.zip
	zip -r perst4net.zip Perst4.NET
