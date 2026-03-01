# 公式 .NET SDK イメージを使用
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build

WORKDIR /app

# csproj と依存関係をコピーして restore
COPY *.csproj ./
RUN dotnet restore

# 残りのファイルをコピーしてビルド
COPY . ./
RUN dotnet publish -c Release -o out

# ランタイム用イメージ
FROM mcr.microsoft.com/dotnet/aspnet:7.0
WORKDIR /app
COPY --from=build /app/out ./

# Web API を起動
ENTRYPOINT ["dotnet", "LineCalcBot.dll"]