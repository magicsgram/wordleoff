rm single_file.tar.xz
dotnet publish -c Release -r linux-x64 --self-contained=true -p:IncludeAllContentForSelfExtract=true -p:PublishTrimmed=true -o ./webpublish
XZ_OPT=-9 tar -Jcvf single_file.tar.xz webpublish/
rm -rf webpublish/
