rm -rf out
rm -rf server
mkdir -p server
cp ../horizon-server server -r


docker build . -t uya_plugin

docker run -v "${PWD}/out/":/mnt/out -it uya_plugin

chmod a+rw out -R

cp out/medius/* ../horizon-server/docker/medius_plugins
cp out/dme/* ../horizon-server/docker/dme_plugins
