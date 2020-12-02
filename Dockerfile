FROM mcr.microsoft.com/dotnet/aspnet:5.0
COPY bin/Release/net5.0/publish/ App/
WORKDIR /App
EXPOSE 8080
ENTRYPOINT ["dotnet", "suaveNetCore5.dll"]

