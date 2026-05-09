# Беремо образ .NET SDK, щоб зібрати наш проект
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# Копіюємо файли проекту та відновлюємо залежності
COPY . ./
RUN dotnet restore
RUN dotnet publish -c Release -o out

# Беремо чистий образ ASP.NET Runtime, щоб запустити наш проект
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /app/out .

# Встановлюємо змінну середовища для Production
ENV ASPNETCORE_ENVIRONMENT=Production

# Вказуємо порт, який буде слухати наш додаток
EXPOSE 8080

# Команда для запуску нашої програми
ENTRYPOINT ["dotnet", "Beckend.dll"]