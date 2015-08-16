!IFNDEF PERST_LIBRARY
PERST_LIBRARY=..\bin\PerstNet20.dll
!ENDIF

!IFNDEF DXROOT
DXROOT=C:\Program Files\Sandcastle\
!ENDIF

!IFNDEF DEBUG
DEBUG=1
!ENDIF

CSC_FLAGS=/w:2 /nologo /unsafe- /checked- 
GENDOC=/doc:doc\assocdb.xml
DEFS=DEBUG

!IF $(DEBUG)
DEFS=$(DEFS),DEBUG
CSC_FLAGS=$(CSC_FLAGS) /debug+
!ENDIF

CSC=csc $(CSC_FLAGS) /r:$(PERST_LIBRARY) /d:$(DEFS)

LIBRARY=bin\\AssocDB.dll

EXAMPLES=tst/Library/bin/debug/Library.exe tst/Hospital/bin/debug/Hospital.exe 
SOURCES=src\\*.cs


all: library examples

library: $(LIBRARY)

documentation: library
	-md working
	cd working
	copy ..\doc\assocdb.xml comments.xml
	MRefBuilder ../bin/AssocDB.dll /dep:..\$(PERST_LIBRARY) /out:reflection.org
	XslTransform /xsl:"$(DXROOT)ProductionTransforms\ApplyVSDocModel.xsl" reflection.org /xsl:"$(DXROOT)ProductionTransforms\AddFriendlyFilenames.xsl" /out:reflection.xml
	XslTransform /xsl:"$(DXROOT)ProductionTransforms\ReflectionToManifest.xsl" reflection.xml /out:manifest.xml
	call "$(DXROOT)Presentation\vs2005\copyOutput.bat"
	BuildAssembler /config:..\..\sandcastle.config manifest.xml
	XslTransform /xsl:"$(DXROOT)ProductionTransforms\ReflectionToChmProject.xsl" reflection.xml /out:Output\test.hhp
	XslTransform /xsl:"$(DXROOT)ProductionTransforms\createvstoc.xsl" reflection.xml /out:toc.xml 
	XslTransform /xsl:"$(DXROOT)ProductionTransforms\TocToChmContents.xsl" toc.xml /out:Output\test.hhc
	XslTransform /xsl:"$(DXROOT)ProductionTransforms\ReflectionToChmIndex.xsl" reflection.xml /out:Output\test.hhk
	-"C:\Program Files\HTML Help Workshop\hhc.exe" Output\test.hhp 
	move /y Output\test.chm ..\doc\AssocDB.chm
	cd ..
	rd /s/q working


$(LIBRARY): $(SOURCES)
	-md bin
	-md doc
	$(CSC) /target:library /out:$(LIBRARY) $(GENDOC) $(SOURCES)

examples: $(EXAMPLES)

tst/Library/bin/debug/Library.exe: tst/Library/Library.cs $(LIBRARY)
	-md tst\Library\bin\debug
	copy $(LIBRARY) tst\Library\bin\debug
	$(CSC) /r:$(LIBRARY) /out:tst/Library/bin/debug/Library.exe tst\\Library\\Library.cs

tst/Hospital/bin/debug/Hospital.exe: tst/Hospital/Hospital.cs $(LIBRARY)
	-md tst\Hospital\bin\debug
	copy $(LIBRARY) tst\Hospital\bin\debug
	$(CSC) /r:$(LIBRARY) /out:tst/Hospital/bin/debug/Hospital.exe tst\\Hospital\\Hospital.cs

clean:
	-rd /s/q obj
	-rd /s/q tst\Library\obj
	-rd /s/q tst\Library\bin
	-rd /s/q tst\Hospital\obj
	-rd /s/q tst\Hospital\bin
	-del /q *.dbs
	-del /q *.xml
	-del /q *.suo
	-del /q *.ncb
