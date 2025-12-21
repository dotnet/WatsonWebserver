@echo off

set /A counter = 0;
set /A limit = 10;
if [%1] == [] (set /A limit = 10) else (set /A limit = %1)

:start
set /A counter +=1
if %counter% EQU %limit% goto end
echo Running task %counter%
start curl http://localhost:8080 -v
goto start

:end
echo Executed %counter% iterations!

@echo on
