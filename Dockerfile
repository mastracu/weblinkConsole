FROM mcr.microsoft.com/dotnet/aspnet:9.0
COPY publish/ App/
COPY wwwroot/ App/wwwroot
COPY json/ App/json
WORKDIR /App
EXPOSE 8083
ENTRYPOINT ["dotnet", "weblinkConsole.dll"]

