FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props OptionsDesk.slnx ./
COPY src/OptionsDesk.Pricing/OptionsDesk.Pricing.csproj src/OptionsDesk.Pricing/
COPY src/OptionsDesk.Data/OptionsDesk.Data.csproj src/OptionsDesk.Data/
COPY src/OptionsDesk.Web/OptionsDesk.Web.csproj src/OptionsDesk.Web/
RUN dotnet restore src/OptionsDesk.Web/OptionsDesk.Web.csproj
COPY src/ src/
RUN dotnet publish src/OptionsDesk.Web/OptionsDesk.Web.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
RUN mkdir /data && chown app /data
USER app
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Desk="Data Source=/data/desk.db"
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "OptionsDesk.Web.dll"]
