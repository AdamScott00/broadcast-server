dotnet publish -c Release -r osx-arm64 --self-contained true -o ./publish -p:PublishSingleFile=true
sudo cp ./publish/broadcast-server /usr/local/bin/broadcast-server
chmod +x /usr/local/bin/broadcast-server
