Remove-Item -Recurse -Force ./out
Remove-Item -Recurse -Force ./server
Remove-Item -Recurse -Force ./out/medius
Remove-Item -Recurse -Force ./out/dme
mkdir ./out/medius
mkdir ./out/dme
mkdir ./server

#cp ../horizon-server server -r
Copy-Item ../horizon-server -Destination ./server -Force -Recurse

docker build . -t uya_plugin

docker run -v ${PWD}/out/:/mnt/out -it uya_plugin

sudo chmod a+rw out -R

