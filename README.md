# Payroll & HR Integration Dashboard
## Oversikt

Dette prosjektet er et Payroll- og HR-integrasjonsdashboard bygget med ASP.NET Core (MVC). Det ble laget på 2 dager for å demonstrere evne. (Dette er ett eksperiment og ikke en komplett løsning)

Løsningen er laget for å vise hvordan reelle lønns- og HR-integrasjoner overvåkes, valideres og driftes.

### Målgruppe:
- HR-avdelinger
- Lønnskonsulenter
- Integrasjonsspesialister

### Fokusområder:
- Transparens
- Datakvalitet
- Operasjonell innsikt
- Trygge lønnseksporter

### Viktige funksjoner
- Integrasjonsdrevet dashboard
- Klikkbare ansatte med interaktiv lønnsfordeling
- Datavalidering og kvalitetskontroll
- Simulert multikunde-støtte
- GraphQL brukt som infrastruktur
- Ekstern API-integrasjon (værdata)

### Teknologi
- ASP.NET Core MVC
- Entity Framework Core
- SQLite
- HotChocolate GraphQL
- Chart.js
- Bootstrap 5

## Oppsett
Installer EF-verktøy
```bash
dotnet tool install --global dotnet-ef
```

Kjør prosjektet
```bash
dotnet restore
dotnet ef database update
dotnet run
```

Åpne:
```
http://localhost:XXXX (Sjekk terminal)
```
