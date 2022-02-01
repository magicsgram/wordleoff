wsl rm single_file.tar.xz
start /B /low /wait dotnet publish -c Release -r linux-x64 --self-contained=true -p:IncludeAllContentForSelfExtract=true -p:PublishTrimmed=true -o ./webpublish
wsl XZ_OPT=-9 tar -Jcvf single_file.tar.xz webpublish/
wsl rm -rf webpublish/
