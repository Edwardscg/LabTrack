FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY LabTrack/LabTrack.csproj LabTrack/
RUN dotnet restore LabTrack/LabTrack.csproj
COPY LabTrack/ LabTrack/
RUN dotnet publish LabTrack/LabTrack.csproj -c Release --no-restore -o /publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /publish/ .
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R app:app /home/app/.aspnet
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "LabTrack.dll"]
