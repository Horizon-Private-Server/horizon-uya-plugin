#!/bin/bash
set -e

sudo rm -rf out
sudo rm -rf server
sudo rm -rf out/medius
sudo rm -rf out/dme
mkdir -p out/medius
mkdir -p out/dme
mkdir -p server

cp ../horizon-server server -r

docker build . -t uya_plugin

docker run -v "${PWD}/out/":/mnt/out -it uya_plugin

sudo chmod a+rw out -R

