@rem APP_NAME is the name of the built dll
@set APP_NAME=%1

@rem SRC_DIR is the output directory where VS compiles the DLL into
@set SRC_DIR=%2
@set SRC_DIR=%SRC_DIR:"=%

@set PDB2MDBPATH="F:\AssetStore Projects\Tools\pdb2mdb"
@set VCPROJPATH=%3
@if (%VCPROJPATH%) == () GOTO Label_MissingVariableVCPROJPATH

@rem DST_DIR is the destination directory where the dll's are copied to
@set DST_DIR=C:\Users\crash\Documents\unityheapexplorer\UnityDLL\Assets\HeapExplorer\Editor
@set DST_DIR=%DST_DIR:"=%

@rem create directory if it doesnt exist yet
@if not exist "%DST_DIR%" ( @mkdir "%DST_DIR%" )


@rem @echo Deleting existing files...
@del "%DST_DIR%\*.dll"
@del "%DST_DIR%\*.dll.Unity?"
@del "%DST_DIR%\*.mdb"
@del "%DST_DIR%\*.mdb.Unity?"
@del "%DST_DIR%\%APP_NAME%MenuItem.cs"
@del "%DST_DIR%\%APP_NAME%Source.zip"
@del "%SRC_DIR%\*.mdb"

@rem Copy and rename dlls and mdbs to dest folder

@if exist %SRC_DIR% (
	@pushd %PDB2MDBPATH%
	@echo %SRC_DIR%\%APP_NAME%.dll
	pdb2mdb.exe "%SRC_DIR%\%APP_NAME%.dll"
	@popd

	copy /Y "%SRC_DIR%%APP_NAME%.dll"      "%DST_DIR%\%APP_NAME%.dll"
	copy /Y "%SRC_DIR%%APP_NAME%.dll.mdb"  "%DST_DIR%\%APP_NAME%.dll.mdb"
)
	
	
goto Label_DONE

:Label_MissingVariableVCPROJPATH
@echo ERROR: Missing 'VCPROJPATH' variable passed to batch file. VCPROJPATH is considered to be in the 3rd argument.
exit /b 1

:Label_DONE
