# Build stage =========================================================================
FROM mcr.microsoft.com/dotnet/core/sdk:3.1 as builder

COPY . /src

# Remove the xcopy commmand
RUN sed -i '42,44d' /src/Horizon.Plugin.UYA/Horizon.Plugin.UYA.csproj
RUN sed -i '36,38d' /src/Horizon.Plugin.UYA.Dme/Horizon.Plugin.UYA.Dme.csproj

#====== Build DME
WORKDIR /src/server/horizon-server/Server.Dme
RUN dotnet publish -c Release -o /server

#===== Build MAS/MLS/NAT
WORKDIR /src/server/horizon-server/Server.Medius
RUN dotnet publish -c Release -o /server

RUN cp /server/*.dll /src/Horizon.Plugin.UYA/
RUN cp /server/*.dll /src/Horizon.Plugin.UYA.Dme/

#====== Build DME
WORKDIR /src/Horizon.Plugin.UYA
RUN dotnet publish -c Release -o /out/medius
#RUN rm -rf /out/medius/runtimes/osx-x64  
#RUN rm -rf /out/medius/runtimes/win-x64  
#RUN rm -rf /out/medius/runtimes/win-x86  

#===== Build MAS/MLS/NAT
WORKDIR /src/Horizon.Plugin.UYA.Dme
RUN dotnet publish -c Release -o /out/dme

CMD "/src/entrypoint.sh"
