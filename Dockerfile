FROM mcr.microsoft.com/dotnet/aspnet:5.0
COPY bin/Release/net5.0/publish/ App/
COPY wwwroot/ App/wwwroot
COPY json/ App/json
WORKDIR /App
EXPOSE 8083
ENTRYPOINT ["dotnet", "weblinkConsole.dll"]

