# Setup Guide — Local Failover PoC
This guide explains how to set up the project and databases from scratch.  
It covers everything required to get both the **Local** and **Cloud** roles running without errors.



## 1) Prerequisites

- .NET 8 SDK
- Visual Studio 2022 **or** VS Code
- SQL Server **LocalDB** (via Visual Studio Installer)
- Erlang + RabbitMQ (RabbitMQ UI on http://localhost:15672)



## 2) Create & start LocalDB instances

Create and start a **named LocalDB instance** for each role. Use:
- `localfailoverdb` → Local role
- `cloudfailoverdb` → Cloud role

Commands (choose the instance name per role):

```powershell
sqllocaldb create "[instance name]"
sqllocaldb start  "[instance name]"
sqllocaldb info   "[instance name]"
```


## 3) Create the databases inside the instances

Connect to the instance using VS Code (MSSQL extension) or SSMS and run:

*Locally:*
```sql
CREATE DATABASE [ErpLocal];
```
*Cloud:*
```sql
CREATE DATABASE [ErpCloud];
```



## 4) Configure appsettings (connection string only)

Check if the `ConnectionStrings:Db` in the local and cloud settings is set correctly. This is based on the names used in setting the database.  

When following this readme, values should read the following.

*Locally:*
- `instanceName = localfailoverdb`
- `databaseName = ErpLocal`

*Cloud:*
- `instanceName = cloudfailoverdb`
- `databaseName = ErpCloud`

The **instance** and **database** are shown in the template below:

```json
"Db": "Server=(localdb)\\[instanceName];Database=[databaseName];Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
```





<!-- Place these in the project root:
- `appsettings.json` (base)
- `appsettings.Local.json` (Local role)
- `appsettings.Cloud.json` (Cloud role) -->



## 5) EF tooling (once per machine - once per project)

Install EF CLI (machine-wide) and the design package (project):

```powershell
dotnet tool install --global dotnet-ef
```
<!-- dotnet add package Microsoft.EntityFrameworkCore.Design -->

<!-- The codebase includes a **design-time factory** (`ErpDbContextFactory`) so EF CLI can find the right connection string per environment. -->



## 6) Create schema (migrations) and apply

From the API project folder (same folder as the `.csproj`):

*Local DB:*

  ```powershell
  $Env:ASPNETCORE_ENVIRONMENT = "Local"
  dotnet ef database update
  ```

*Cloud DB:*

  ```powershell
  $Env:ASPNETCORE_ENVIRONMENT = "Cloud"
  dotnet ef database update
  ```

<!-- > If EF reports “pending model changes”, run another migration with a new name and then `dotnet ef database update`. -->
## 7) Starting the application

You can start either the **Local** or **Cloud** role using the launch profiles defined in `Properties/launchSettings.json`. 


```powershell
# Start Local API (uses appsettings.Local.json)
dotnet run --launch-profile "Local"

# Start Cloud API (uses appsettings.Cloud.json)
dotnet run --launch-profile "Cloud"
```

Alternatively you can run without profiles if the terminal's ENV is set: `echo $Env:ASPNETCORE_ENVIRONMENT`.

7) voor snelle db reset: `dotnet ef database drop -f && dotnet ef database update` 

<!-- ## 7) Quick sanity checks

- Environment variable in this terminal:

  ```powershell
  echo $Env:ASPNETCORE_ENVIRONMENT   # Local or Cloud
  ```

- EF sees your context/connection:

  ```powershell
  dotnet ef dbcontext info
  ``` -->

