FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY AccessControl/AccessControl.csproj AccessControl/
RUN dotnet restore AccessControl/AccessControl.csproj
COPY AccessControl/ AccessControl/
RUN dotnet publish AccessControl/AccessControl.csproj -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p /app/wwwroot/uploads && chown -R app:app /app/wwwroot
USER app
ENV ASPNETCORE_URLS=http://+:8080 ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "AccessControl.dll"]
