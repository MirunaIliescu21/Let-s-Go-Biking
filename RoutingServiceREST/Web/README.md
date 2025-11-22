### 🧩 Etapa 1 – Implementare și rulare în Visual Studio

Pentru claritate și simplitate în faza de dezvoltare, am început proiectul sub forma a două **WCF Service Library Projects**:

* **ProxyCacheService** – un serviciu SOAP care acționează ca *proxy* și *cache generic* pentru apelurile către API-uri externe.
  Implementarea include clasele:

  * `GenericProxyCache<T>` – gestionarea cache-ului generic;
  * `HttpGetResource` – clasa care realizează cererea HTTP reală;
  * `ProxyService` și `IProxyService` – expunerea logicii prin SOAP.

* **RoutingServiceREST** – un serviciu REST care apelează ProxyCacheService pentru a obține datele necesare rutării (de exemplu, stațiile JCDecaux).
  Implementarea include:

  * `RoutingServiceREST` și `IRoutingServiceREST` – endpoint-urile REST expuse.

Pentru a facilita testarea și depanarea, am păstrat configurațiile în fișierele `App.config` generate de Visual Studio, fiecare serviciu având propriul său port:

* `ProxyCacheService`: [http://localhost:8734](http://localhost:8734)
* `RoutingServiceREST`: [http://localhost:8733](http://localhost:8733)

Am folosit **WCF Service Host** și **WCF Test Client** pentru a porni și testa serviciile în paralel, verificând comunicarea între REST și SOAP.

Această variantă „de dezvoltare” a fost folosită pentru a valida funcționalitatea serviciilor înainte de a le transforma în versiuni **self-hosted** independente (.exe), conform cerinței finale.



Yes — output-ul tău „Lyon Part-Dieu → Bellecour” e perfect 🎉.
Ce vezi la București / „miruna” vine din faptul că noi căutăm „cea mai apropiată stație JCDecaux din toată lumea”, fără un prag de proximitate. Asta te duce la contractul **Maribor** (Slovenia) 😅

Hai să reparăm cu niște **garduri (thresholds)** și validări clare:

* dacă **nu există stație JCDecaux la < 5 km** de origine/destinație ⇒ „Nu există rețea JCDecaux utilă…”
* dacă stația de ridicare e la **> 1.2 km** de origine sau stația de predare e la **> 1.2 km** de destinație ⇒ „stația e prea departe…”
* dacă stațiile cele mai apropiate de origine/destinație sunt în **contracte diferite** și sunt la **> 30 km** una de alta ⇒ „orig/dest în rețele diferite (inter-city) – neacceptat în MVP”

### 🔧 Modificări exacte (doar în `RoutingServiceREST.cs`)

1. **Adaugă constantele** în clasa `RoutingService` (sus, lângă restul helperilor):

```csharp
// Praguri de proximitate (tunable)
private const double MAX_NETWORK_RADIUS_KM = 5.0;   // minim pentru a spune "există rețea în zonă"
private const double MAX_ORIGIN_RADIUS_KM  = 1.2;   // cât accepti să mergi pe jos până la stația de preluare
private const double MAX_DEST_RADIUS_KM    = 1.2;   // cât accepti să mergi pe jos de la stația de predare
private const double MAX_INTERCITY_KM      = 30.0;  // distanță max. între stația cea mai apropiată de orig. și cea de dest.
```

2. **După ce calculezi stațiile cele mai apropiate** (imediat după:

```csharp
var nearestToOrigin = FindClosestStation(allStations, request.OriginLat, request.OriginLon);
var nearestToDest   = FindClosestStation(allStations, request.DestLat,   request.DestLon);
```

), **inserează:**

```csharp
// distanța până la cele mai apropiate stații JCDecaux (orice contract)
double distNetOriginKm = HaversineKm(request.OriginLat, request.OriginLon,
                                     nearestToOrigin.Position.Lat, nearestToOrigin.Position.Lon);
double distNetDestKm   = HaversineKm(request.DestLat,   request.DestLon,
                                     nearestToDest.Position.Lat,   nearestToDest.Position.Lon);

// dacă nu există rețea rezonabil aproape, anunțăm explicit
if (distNetOriginKm > MAX_NETWORK_RADIUS_KM)
    return Fail("Nu există rețea JCDecaux utilă în zona Originii.");
if (distNetDestKm > MAX_NETWORK_RADIUS_KM)
    return Fail("Nu există rețea JCDecaux utilă în zona Destinației.");

// dacă rețeaua detectată lângă origine și cea lângă destinație sunt în orașe diferite și mult depărtate,
// considerăm că nu e un scenariu suportat de MVP (inter-city)
double nearestStationsDistanceKm = HaversineKm(nearestToOrigin.Position.Lat, nearestToOrigin.Position.Lon,
                                               nearestToDest.Position.Lat,   nearestToDest.Position.Lon);
if (!string.Equals(nearestToOrigin.ContractName, nearestToDest.ContractName, StringComparison.OrdinalIgnoreCase)
    && nearestStationsDistanceKm > MAX_INTERCITY_KM)
{
    return Fail("Originea și destinația par să fie în rețele JCDecaux diferite, prea departe una de alta (inter-city).");
}
```

3. **După ce alegi efectiv `bikeFrom` și `bikeTo`**, adaugă verificarea „stația e prea departe de tine”:

```csharp
double walkToPickupKm = HaversineKm(request.OriginLat, request.OriginLon,
                                    bikeFrom.Position.Lat, bikeFrom.Position.Lon);
double walkFromDropKm = HaversineKm(bikeTo.Position.Lat, bikeTo.Position.Lon,
                                    request.DestLat, request.DestLon);

if (walkToPickupKm > MAX_ORIGIN_RADIUS_KM)
    return Fail($"Cea mai apropiată stație cu biciclete este prea departe de origine ({walkToPickupKm:F2} km).");
if (walkFromDropKm > MAX_DEST_RADIUS_KM)
    return Fail($"Cea mai apropiată stație cu locuri libere este prea departe de destinație ({walkFromDropKm:F2} km).");
```

> Atât. Nimic altceva. Logica ta existentă rămâne; doar punem garduri ca să nu mai „fugă” la Maribor pentru București sau să accepte mers 8 km pe jos până la stație.

---

## ✅ Ce rezultate vei obține acum pentru cazurile tale

* **București → București**
  `Success: false`, `Message: "Nu există rețea JCDecaux utilă în zona Originii."` (și/sau Destinației).

* **Brindas → Lyon Bellecour**
  cel mai probabil: `Success: false`, `Message: "Cea mai apropiată stație cu biciclete este prea departe de origine (~X km)."`
  (Brindas e la >10 km de stații Lyon; nu vrem să-l plimbăm atâta pe jos în MVP.)

* **Brindas → miruna**
  ori pică la „Nu există rețea…”, ori la „inter-city” (dacă geocodarea „miruna” iese în altă țară), în orice caz nu va mai genera trasee absurde.

---

## 🧪 Testează din nou (POST–uri gata de folosit)

1. **Fără rețea (Bucharest):**

```json
{ "Origin": "Piata Victoriei, Bucharest", "Destination": "Piata Universitatii, Bucharest" }
```

2. **Stații prea departe (Brindas → Lyon):**

```json
{ "Origin": "Brindas, France", "Destination": "Lyon Bellecour" }
```

3. **Destinație „dubioasă” (inter-city):**

```json
{ "Origin": "Lyon Part-Dieu", "Destination": "miruna" }
```

4. **Caz bun (Lyon):**

```json
{ "Origin": "Lyon Part-Dieu", "Destination": "Place Bellecour" }
```

5. **Caz pietonal (Paris scurt):**

```json
{
  "Origin": "Paris", "Destination": "Louvre",
  "OriginLat": 48.8566, "OriginLon": 2.3522,
  "DestLat": 48.8606, "DestLon": 2.3376
}
```

---

## 💬 Despre cazul 6 (lipsă biciclete/locuri)

Asta depinde de starea reală a stațiilor. Pentru a **simula** rapid:

* în `FindClosestStationWithBikes` schimbă temporar filtrul la `> 1000` biciclete (nu va găsi nimic) → vei obține:
  `Success: false`, `Message: "Nu există stații cu biciclete/locuri libere în apropiere."`

sau

* forțează `stations.Where(s => 0 > 0)` (mereu false) ca să testezi ramura de fallback.

---

Dacă vrei, pot să-ți trimit `RoutingServiceREST.cs` deja „lipit” cu aceste trei inserții ca să dai paste 1:1.


# Let's Go Biking – Routing Service (MVP)

A WCF REST service that computes itineraries maximizing JCDecaux bike usage when faster than walking. A separate SOAP Proxy/Cache service performs outgoing HTTP requests and caches responses to protect external APIs.

## Architecture

- **RoutingServiceREST (WCF, REST)**  
  Single endpoint `POST /itinerary` that:
  1. Geocodes origin/destination (OpenRouteService)  
  2. Retrieves JCDecaux stations (via Proxy/Cache)  
  3. Picks the closest usable stations (bikes at origin, free stands at destination)  
  4. Compares *walk-only* vs *walk → bike → walk* using ORS durations  
  5. Returns step hints and timing

- **ProxyCacheService (WCF, SOAP)**  
  A proxy that executes REST calls with in-memory caching (C# `MemoryCache`).  
  It reduces rate-limit issues and centralizes external access.  
  Optionally, it can attach the ORS Bearer header so the routing project avoids embedding the ORS key.

## Prerequisites

- Visual Studio 2022 (or later), .NET Framework (WCF)
- NuGet: `Newtonsoft.Json`
- API keys:
  - JCDecaux: https://developer.jcdecaux.com
  - OpenRouteService: https://openrouteservice.org

> **Do not hardcode keys.** Put them in `App.config`:
```xml
<appSettings>
  <add key="JCDECAUX_API_KEY" value="YOUR_JCDECAUX_KEY"/>
  <add key="ORS_API_KEY" value="YOUR_ORS_KEY"/>
</appSettings>


Perfect — am înțeles exact contextul și structura.
Ți-am redactat un **README complet în engleză**, bine organizat, documentat și potrivit pentru predare / GitHub.
Include explicațiile pentru alegerea arhitecturii WCF, cum rulează serviciile, ce urmează în etapa „Console App”, plus detalii despre fișiere și API-uri.

---

# 🚲 Let's Go Biking — Routing & Proxy Services (MVP)

## 📘 Overview

This project is part of the *“Let’s Go Biking”* assignment: a routing system that computes the most efficient itinerary using **JCDecaux** bikes when faster than walking.

It is implemented in **C# (.NET Framework, WCF)** and currently consists of two interdependent services:

* `ProxyCacheService` — a **SOAP service** acting as a generic HTTP proxy and caching layer.
* `RoutingServiceREST` — a **REST service** that computes routes, consuming data from `ProxyCacheService`.

The MVP can already:

* Retrieve JCDecaux station data (via proxy cache),
* Geocode addresses using OpenRouteService (ORS),
* Compare *walk-only* vs *bike-plan* routes (walk → bike → walk),
* Return timing, distance, and instructions,
* Handle ambiguous locations and missing networks.

---

## 🧩 Architecture

### 1. **ProxyCacheService**

A WCF **SOAP** service responsible for all outgoing HTTP requests and caching.

**Main components:**

* `GenericProxyCache<T>` — a reusable caching system based on `MemoryCache`, supporting TTL and absolute expiration.
* `HttpGetResource` — performs real HTTP GET requests and stores text content.
* `ProxyService` / `IProxyService` — expose SOAP operations that RoutingServiceREST calls.
* `App.config` — defines the SOAP endpoints and binding configuration.

**Role:**
It isolates all external API access (JCDecaux, ORS, etc.) so that RoutingServiceREST never calls the Internet directly. This prevents API rate limits and improves reusability for future projects.

---

### 2. **RoutingServiceREST**

A WCF **REST** service that exposes a single main endpoint:

#### `POST /itinerary`

Given two addresses or GPS coordinates:

1. **Geocodes** them via OpenRouteService (ORS);
2. **Retrieves** JCDecaux stations from the proxy (cached);
3. **Finds** the closest stations with bikes and free stands;
4. **Compares** walking-only vs biking time and distance;
5. **Returns** an itinerary plan including:

   * whether to use a bike,
   * walking/biking durations,
   * total distances,
   * JCDecaux contracts and station names,
   * optional debug info (coordinates, top-3 station candidates).

#### Other endpoints

* `GET /contracts` — returns all JCDecaux contracts.
* `GET /stations?contract=` — returns all stations for a given contract.
* `GET /ping` — test endpoint returning `"pong"`.

**Main files:**

* `RoutingService.cs` — logic implementation (geocoding, routing, comparison).
* `IRoutingServiceREST.cs` — REST interface declaration.
* `Dto.cs` — all request/response data contracts:

  * `ItineraryRequest`
  * `ItineraryResponse`
  * JCDecaux models (`JcdecauxStation`, `Position`, etc.)
* `App.config` — REST endpoint and binding definitions.

---

## ⚙️ Implementation Details

### 🌐 External APIs

| API                        | Purpose                       | Notes                       |
| -------------------------- | ----------------------------- | --------------------------- |
| **JCDecaux VLS v3**        | Station data (bikes & stands) | Cached via Proxy            |
| **OpenRouteService (ORS)** | Geocoding & routing           | Bearer key handled in proxy |

### 🔒 API Keys

Keys are stored locally in configuration for development:

```xml
<appSettings>
  <add key="JCDECAUX_API_KEY" value="your_jcdecaux_key"/>
  <add key="ORS_API_KEY" value="your_ors_key"/>
</appSettings>
```

---

## 🧠 Design Rationale

During the **development stage**, both services were created as
**WCF Service Library projects**, not Console Apps.

**Why?**

* Easy debugging via Visual Studio’s built-in *WCF Service Host*;
* Integrated *WCF Test Client* for calling endpoints interactively;
* Simplified development and configuration (App.config);
* Allows verifying SOAP–REST communication before self-hosting.

Both projects can be started from Visual Studio:

* Set **RoutingServiceREST** as the startup project (depends on ProxyCacheService);
* Then manually start `ProxyCacheService` → *Debug → Start new instance*.

**Ports (default):**

* ProxyCacheService → `http://localhost:8734/`
* RoutingServiceREST → `http://localhost:8733/`

---

## 🧩 Key Functionalities

| Category                   | Description                                                                                    |
| -------------------------- | ---------------------------------------------------------------------------------------------- |
| **Geocoding**              | Uses ORS to convert addresses → coordinates, with disambiguation (try near origin).            |
| **Network validation**     | Ensures JCDecaux network exists near both points (<5 km).                                      |
| **Bike station selection** | Finds nearest station with bikes and nearest with free stands.                                 |
| **Thresholds**             | Rejects plans if stations are >1.2 km from origin/destination or in different cities (>30 km). |
| **Comparison**             | Computes both plans (walk-only vs bike-plan) and keeps the fastest.                            |
| **Caching**                | ProxyCacheService caches JCDecaux & ORS results to reduce API load.                            |
| **Fallbacks**              | If JCDecaux unavailable → auto “walk-only” fallback.                                           |
| **Debug mode**             | Returns coordinates, top-3 station candidates, and intermediate distances.                     |

---

## 🗺️ Front-End Demo

A minimal static front-end (`Web/index.html`) built with **Leaflet.js**:

* Calls the REST endpoint via `fetch` (`POST /itinerary`).
* Displays:

  * origin/destination markers,
  * stations for pickup/dropoff,
  * route segments (walk → bike → walk) with colors.
* Uses OpenStreetMap tiles.
* Runs via **Live Server** in VS Code (`127.0.0.1:5500/index.html`).

---

## 🔧 Next Steps – Self-Hosted Transformation

The next milestone is to **convert both WCF libraries into self-hosted Console Apps**.
This will allow launching each service independently, without Visual Studio.

Each project will gain a `Program.cs` file:

```csharp
using System;
using System.ServiceModel;
using ProxyCacheService;  // or RoutingServiceREST

class Program
{
    static void Main()
    {
        using (var host = new ServiceHost(typeof(ProxyService)))
        {
            host.Open();
            Console.WriteLine("ProxyCacheService running...");
            Console.ReadLine();
        }
    }   
}
```

This replicates the configuration from `App.config` directly in code using `System.ServiceModel` bindings.
When built, each project produces a standalone `.exe` in `bin/Debug/`, runnable from any terminal.

---

## 🧪 Testing & Debugging

* **Postman:**
  Test REST endpoints (`POST /itinerary`, `GET /contracts`, `GET /stations`).
* **Browser (frontend):**
  `index.html` → calls the same routes and displays the path.
* **WCF Test Client:**
  Used to validate SOAP operations (`ProxyCacheService`).

Example request (JSON):

```json
{
  "Origin": "Place du Capitole, Toulouse",
  "Destination": "Gare Matabiau, Toulouse",
  "Debug": true
}
```

Example response:

```json
{
  "Success": true,
  "UseBike": true,
  "Message": "MVP OK",
  "OriginContract": "toulouse",
  "BikeFrom": "00006 - LOIS - CAPITOL",
  "BikeTo": "00097 - BONREPOS - RAYMOND IV",
  "WalkOnlyDurationSec": 966.9,
  "BikePlanDurationSec": 647.2,
  "WalkOnlyDistanceMeters": 1200.5,
  "BikePlanDistanceMeters": 890.2
}
```

---

## 🧭 Folder Structure

```
RoutingServiceREST.sln
├── ProxyCacheService/
│   ├── Caching/
│   │   ├── GenericProxyCache.cs
│   │   └── HttpGetResource.cs
│   ├── ProxyService.cs
│   ├── IProxyService.cs
│   └── App.config
│
├── RoutingServiceREST/
│   ├── Dto.cs
│   ├── IRoutingServiceREST.cs
│   ├── RoutingServiceREST.cs
│   ├── App.config
│   ├── Web/
│   │   └── index.html
│
└── packages/
```

---

## 📄 Summary

| Component                      | Type              | Purpose                                    |
| ------------------------------ | ----------------- | ------------------------------------------ |
| **ProxyCacheService**          | SOAP (WCF)        | Performs and caches external REST calls.   |
| **RoutingServiceREST**         | REST (WCF)        | Computes bike/walk itineraries.            |
| **Web Frontend**               | HTML/JS (Leaflet) | Visual interface for testing the REST API. |
| **App.config**                 | XML               | WCF endpoint/binding configuration.        |
| **Program.cs** *(to be added)* | C#                | Entry point for self-hosted version.       |

---

## 🚀 Future Improvements

* Add **Polyline geometry** visualization (already in progress).
* Include **distance per segment** (walk/bike/walk) — implemented in current version.
* Transform both services to **Console-hosted WCF servers**.
* Store API keys in environment variables or secrets manager.
* Implement persistence for caching (optional file-based MemoryCache).

---

Would you like me to append a **short “How to Run (Step-by-Step)”** section at the end — with exact Visual Studio launch steps, Postman URLs, and front-end testing instructions (like a “Quick Start” guide)?



### Important observation:
If I want to go `from Paris to Lyon` it's posible to take a bike that is in the same contract named "jcdecauxbike"
```
  UseBike: Yes
  Walk-only: 5905 min (0.00 km)  |  Bike-plan: 2496 min (612.42 km)
  Start point: Paris
  End point: Lyon
  The origin and destination are in different JCDecaux networks (inter-city).
  Origin contract: jcdecauxbike
  Walk to station '2017 - LUX PARC EXPO'.
  Ride to station '1000 - TEST BE LYON'.
  Walk to the destination.
```

The proof for this itinerary is in the response for the GET request:
" GET http://localhost:8733/Design_Time_Addresses/RoutingServiceREST/Service1/stations?contract=jcdecauxbike "

```json
    {
        "ContractName": "jcdecauxbike",
        "Name": "2017 - LUX PARC EXPO",
        "Position": {
            "Lat": 48.800454,
            "Lon": 1.948658
        },
        "TotalStands": {
            "Availabilities": {
                "Bikes": 3,
                "Stands": 2
            }
        }
    },
  
    {
        "ContractName": "jcdecauxbike",
        "Name": "1000 - TEST BE LYON",
        "Position": {
            "Lat": 45.780403,
            "Lon": 4.904869
        },
        "TotalStands": {
            "Availabilities": {
                "Bikes": 0,
                "Stands": 1
            }
        }
    }
```
BUT if we want to go `from Lyon to Paris`, the initenary is not reversed because the nearest station to Lyon hasn't free bike at the moment!! For this reason, we can only that a bike in the origin contract "lyon" for leave the city, and after that it's only a walk itinerary. 

```
  UseBike: Yes
  Walk-only: 5905 min (0.00 km)  |  Bike-plan: 5777 min (495.03 km)
  Start point: Lyon
  End point: Paris
  The origin and destination are in different JCDecaux networks (inter-city).
  Origin contract: lyon
  Walk to station '7014 - MARSEILLE / FÉLISSENT'.
  Ride to station '33001 - ALBIGNY - GARE'.
  Walk to the destination.
```

### Bikes indisponible
For example if I want to go `from Plaisir to Orléans` , exist stations that are in the same contract, but not all the stations have disponible bikes.
For this reason, my implementation knows who to choose the itinerary betweeen stations with available bikes /  stands (for leave the bike there). 

```
  UseBike: Yes
  Walk-only: 1509 min (125.76 km)  |  Bike-plan: 547 min (142.93 km)
  Start point: Plaisir
  End point: Orléans
  Origin contract: jcdecauxbike
  Destination contract: jcdecauxbike
  Walk to station '2017 - LUX PARC EXPO'.
  Pick up a bike and ride to station 'SALON RNTP'.
  Drop the bike and walk to the destination.
```
These stations: "501 - BEE PLAISIR" and "2017 - LUX PARC EXPO" are in the same city (our origin city Plaisir) but the first one hasn't disponible bikes.
```json
    {
        "ContractName": "jcdecauxbike",
        "Name": "501 - BEE PLAISIR",
        "Position": {
            "Lat": 48.799648,
            "Lon": 1.945439
        },
        "TotalStands": {
            "Availabilities": {
                "Bikes": 0,
                "Stands": 1
            }
        }
    },

    {
        "ContractName": "jcdecauxbike",
        "Name": "2017 - LUX PARC EXPO",
        "Position": {
            "Lat": 48.800454,
            "Lon": 1.948658
        },
        "TotalStands": {
            "Availabilities": {
                "Bikes": 3,
                "Stands": 2
            }
        }
    },
```

### Multiple bikes for reach between 2 cities

Toulouse -> Lyon

```
UseBike: Yes
Walk-only: 5506 min (458.85 km)  |  Bike-plan: 5409 min (461.70 km)
Start point: Toulouse
End point: Lyon
The origin and destination are in different JCDecaux networks (inter-city).
Origin contract: toulouse
Destination contract: lyon
Walk to station '00012 - PARGAMINIERES - LAKANAL'.
Ride to station '00329 - ATLANTA - CAUNES'.
Walk (inter-city) to station '19001 - ST GENIS L. - MÉDIATHÈQUE'.
Ride to station '7014 - MARSEILLE / FÉLISSENT'.
Walk to the destination.
```


### Bike both ends: `Toulouse -> Paris`
```
  UseBike: Yes
  Walk-only: 8855 min (737.88 km)  |  Bike-plan: 5756 min (971.02 km)
  MVP OK — both-ends bike
  Start point: Toulouse
  End point: Paris
  The origin and destination are in different JCDecaux networks (inter-city).
  The origin and destination are in different JCDecaux networks (inter-city).
  Origin contract: toulouse
  Destination contract: jcdecauxbike
  Walk to station '00012 - PARGAMINIERES - LAKANAL'.
  Ride to station '00318 - EGLISE AUCAMVILLE'.
  Walk (inter-city) to station '1033 - CYCLOBURO BORDEAUX'.
  Ride to station '2019 - LA CHAINE IOD'.
  Walk to the destination.
```


### Multimodal routing (walk + bike)

* The service now **always** returns a route geometry:

  * If no usable bike plan exists, it falls back to **walk-only** and still draws the path.
* For **different JCDecaux contracts (inter-city)** we evaluate:

  1. **Origin-only bike:** walk → bike (origin city) → walk (long)
  2. **Destination-only bike:** walk (long) → bike (destination city) → walk
  3. **Both-ends bike:** walk → bike (origin city) → long walk (inter-city) → bike (destination city) → walk
     *(chosen only if it clearly beats simpler plans)*

### Selection logic

* Baseline is **walk-only** between origin and destination (duration from ORS).
* A bike plan is selected only if it’s **faster than walk-only** (with a small buffer, currently 60s).
* The **both-ends** plan must beat the best single-side plan by a larger margin (currently 120s), to avoid unnecessary complexity for tiny gains.

###  Geocoding

* We bias geocoding toward **cities** for short single-word tokens (e.g., “Lyon”), avoiding random streets named “Lyon”.

### 🧭 Response schema

* Classic fields are preserved (`Walk1Coords`, `BikeCoords`, `Walk2Coords`) for backward compatibility.
* New field: `Segments[]` — supports **any number** of segments. Each item has:

  * `Mode`: `"walk"` or `"bike"`
  * `Contract`: JCDecaux contract for bike segments (if applicable)
  * `FromName`, `ToName`
  * `DistanceMeters`, `DurationSec`
  * `Coords`: polyline ([lat, lon] list)

### Debuggability

* When `Debug=true`, we include the “top-3” station candidates used in the decision.
* We also return resolved coordinates for the addresses.

### Known limitations / next steps

* Station selection is greedy; we plan to upgrade to a small **beam search** with penalties (detours, elevation, low availability).
* We don’t yet chain through **multiple intermediate bike cities**; this is on the roadmap as an optional optimization.
* Availability is read at query time only; time-dependent predictions are out of scope for now.




## 🧾 **Project Structure Overview – RoutingServiceREST (after refactor)**

The project follows a **clean modular architecture** to clearly separate responsibilities between routing logic, external services, and planning strategies.
All classes and folders below are part of the same root namespace: `RoutingServiceREST`.

---

### **📁 Root Level**

| File                     | Description                                                                                                                        |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------- |
| `RoutingServiceREST.cs`  | Main WCF service implementing `IRoutingServiceREST`. Handles incoming REST calls, CORS, and delegates logic to internal services.  |
| `IRoutingServiceREST.cs` | Public service interface defining the REST endpoints (`/itinerary`, `/contracts`, `/stations`, `/ping`, and `OPTIONS`).            |
| `Dto.cs`                 | Contains the data transfer objects (DTOs) such as `ItineraryRequest`, `ItineraryResponse`, `JcdecauxStation`, and related classes. |
| `App.config`             | WCF configuration file (bindings, endpoints, and service behaviors).                                                               |
| `packages.config`        | NuGet dependencies used by the project.                                                                                            |

---

### **📁 Planning/**

Responsible for building and selecting the optimal travel itinerary using JCDecaux bike networks and OpenRouteService (ORS).
Implements the strategy pattern for each possible route configuration.

| File                     | Purpose                                                                                         |
| ------------------------ | ----------------------------------------------------------------------------------------------- |
| `ItineraryPlanner.cs`    | Central planning orchestrator that compares multiple route strategies and chooses the best one. |
| `PlannerOptions.cs`      | Configuration for planning heuristics (buffer times, advantage thresholds).                     |
| `StationContext.cs`      | Context object holding station data and contract information for both origin and destination.   |
| `ResponseBuilders.cs`    | Utility builder that creates detailed `ItineraryResponse` objects for each route type.          |
| `OriginOnlyPlan.cs`      | Strategy: use bike only within the **origin** contract city.                                    |
| `DestinationOnlyPlan.cs` | Strategy: use bike only within the **destination** contract city.                               |
| `BothEndsPlan.cs`        | Strategy: bike segments in both origin and destination cities, connected by an intercity walk.  |
| `WalkOnlyPlan.cs`        | Fallback strategy when biking is not feasible (returns a full walking itinerary).               |

---

### **📁 Services/**

Encapsulates all external service calls and data acquisition layers.
Responsible for talking to JCDecaux APIs, OpenRouteService (ORS), and caching via the proxy.

| File                                                     | Purpose                                                                                          |
| -------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `ProxyFactory.cs`                                        | Creates instances of `ProxyRef.ProxyServiceClient` for cached REST communication.                |
| `IGeocodingService.cs` / `OrsGeocodingService.cs`        | Interface + implementation for ORS geocoding requests (address → coordinates).                   |
| `IRoutingCoreService.cs` / `OrsRoutingService.cs`        | Interface + implementation for ORS route computation (returns distance, duration, and geometry). |
| `IStationRepository.cs` / `JcdecauxStationRepository.cs` | Interface + implementation to fetch and parse JCDecaux station and contract data.                |

---

### **📁 Utils/**

Small stateless helpers for mathematical and spatial computations used by all planning modules.

| File                 | Purpose                                                                              |
| -------------------- | ------------------------------------------------------------------------------------ |
| `GeoMath.cs`         | Haversine-based distance calculations (km, meters).                                  |
| `StationPickers.cs`  | Helpers for selecting closest, bike-available, or stand-available JCDecaux stations. |
| `QueryHeuristics.cs` | Utility functions for identifying city-like queries during geocoding.                |

---

### **📁 Web/**

Frontend resources for testing and visualization.

| File         | Purpose                                                                                    |
| ------------ | ------------------------------------------------------------------------------------------ |
| `index.html` | Demo web interface (runs locally via Live Server) for itinerary testing and map rendering. |
| `README.md`  | Project documentation (this file).                                                         |

---

### **🌐 Overall Workflow**

1. `RoutingServiceREST` receives a REST request (`POST /itinerary`).
2. It calls:

   * `OrsGeocodingService` → to resolve addresses into coordinates.
   * `JcdecauxStationRepository` → to load all available bike stations.
   * `ItineraryPlanner` → to decide the best plan (origin-only, destination-only, both-ends, or walk-only).
3. The selected plan uses `OrsRoutingService` to compute individual route segments.
4. The response (`ItineraryResponse`) includes all coordinates, distances, and textual instructions for display in the frontend map.

------------------------------------------------
## ProxyCacheService — Structure & How it works

This project is a SOAP proxy with an in-memory cache used by `RoutingServiceREST`.
It exposes URL-based caching helpers (for any HTTP GET) and a small **generic cache**
used to demonstrate a reusable pattern with JCDecaux resources.

**Folders & files**

- `Caching/GenericProxyCache.cs` — tiny generic cache wrapper over `MemoryCache`.  
  Keys are arbitrary strings; values are instances created via `new T(key)` with a TTL. :contentReference[oaicite:0]{index=0}
- `Caching/HttpGetResource.cs` — represents one HTTP GET response (text), keeping
  `Url`, `Content`, `CreatedUtc`, and the real `StatusCode`. Adds ORS Bearer automatically. :contentReference[oaicite:1]{index=1}
- `Caching/JcdecauxContractsResource.cs` — materializes `contracts` via JCDecaux API. :contentReference[oaicite:2]{index=2}
- `Caching/JcdecauxStationsResource.cs` — materializes `stations` for a given contract.  
  Expected key: `jc:stations:{contract}`. :contentReference[oaicite:3]{index=3}
- `IProxyService.cs` — WCF contract with:
  `Get`, `GetWithTtl`, `GetWithMeta`, `Evict`, `EvictGeneric`, `Stats`,
  and generic helpers `GetJcdecauxContractsGeneric`, `GetJcdecauxStationsGeneric`. :contentReference[oaicite:4]{index=4}
- `ProxyService.cs` — service implementation. Caching for URL requests happens in
  `GetOrCreate(...)` with **HIT/MISS logging**, success-only caching (`StatusCode==200`),
  and TTL extension when requested. Includes debug counters exposed by `Stats()`. :contentReference[oaicite:5]{index=5}

**Default TTL policy (server-side)**
- JCDecaux **contracts**: 1h (generic cache) → `GetJcdecauxContractsGeneric(3600)`. :contentReference[oaicite:6]{index=6}  
- JCDecaux **stations**: 30s (generic cache) → `GetJcdecauxStationsGeneric(contract, 30)`. :contentReference[oaicite:7]{index=7}  
- URL cache (via `GetWithTtl`) is used by `RoutingServiceREST` services
  (e.g., ORS geocoding 24h, directions 2 min) and logs `URL MISS/HIT`. :contentReference[oaicite:8]{index=8}

**Stats & logs**
- `Stats()` reports counters for the **URL-based cache only** (`hits`, `misses`) and
  `items = MemoryCache.Default.GetCount()` (current number of cached entries, including
  both `GET::...` and `jc:...`). Use console debug logs to see per-request `HIT/MISS`. :contentReference[oaicite:9]{index=9}

**Testing (WCF Test Client / Postman)**
1. Call `GetWithTtl("https://api.jcdecaux.com/vls/v3/contracts", 30)` twice:
   first `URL MISS → MISS->CACHED`, then `URL HIT` within 30s. Logs show keys & TTL. :contentReference[oaicite:10]{index=10}
2. Call `GetJcdecauxStationsGeneric("lyon", 5)` three times: MISS → HIT → (wait 6s) → MISS.  
   Generic cache logs appear as `Cache HIT/MISS for key 'jc:stations:lyon'`. :contentReference[oaicite:11]{index=11}

> **Note:** the frontend never sets TTL; it’s enforced server-side in `RoutingServiceREST`
> when calling the proxy. A “Refresh” action could optionally call `forceRefresh=true`.

-------------------------------------------------
 **Note:**  
 The `Stats()` endpoint reports only the statistics of the URL-based cache
 (used by the `Get`, `GetWithTtl`, and `GetWithMeta` methods).  
 The generic cache (`GenericProxyCache<T>`) for specific data types such as
 JCDecaux contracts and stations maintains its own internal TTL mechanism and
 shows cache behavior through console logs (`Cache HIT/MISS`).  
 This separation keeps the design simple and avoids mixing two independent
 caching systems used for different purposes.

**Caching & TTL policy**
- TTL is enforced server-side inside the Routing Service when calling the Proxy (`GetWithTtl`).
- JCDecaux contracts: 1h
- JCDecaux stations (availability): 30s
- ORS geocoding: 24h
- ORS directions: 2min

- The frontend does not set TTL. **TO DO** Optionally, a “Refresh” action can trigger a server call with `forceRefresh=true`.
- Verification: enable server logs and observe `URL MISS → URL HIT` within the TTL window when calling the same URL.
- `Stats()` reports current URL-cache counters; `items` is the current number of entries in `MemoryCache.Default` (URL + generic).
 

`GetJcdecauxStationsGeneric("lyon", 60)`
`GetJcdecauxStationsGeneric("toulouse", 60)`
`GetJcdecauxStationsGeneric("toulouse", 60)`

Console Output (Debug):

[11:31:41 AM] Cache MISS for key 'jc:stations:lyon'
[11:31:50 AM] Cache MISS for key 'jc:stations:toulouse'
[11:31:55 AM] Cache HIT for key 'jc:stations:toulouse'

Proof that we are catching TTL requests correctly

---------------------------------------------------

`GetWithMeta("https://api.jcdecaux.com/vls/v3/stations?contract=__invalid__", 30)`

-> first call:
"{\"cache\":\"MISS_NO_CACHE\",\"key\":\"GET::https://api.jcdecaux.com/vls/v3/stations?con" +
    "tract=__invalid__&apiKey=13f2c4be0f2297fac09d629d0ca3bebec76b9627\",\"length\":72}"

-> immediately, second call:
"{\"cache\":\"MISS_NO_CACHE\",\"key\":\"GET::https://api.jcdecaux.com/vls/v3/stations?con" +
    "tract=__invalid__&apiKey=13f2c4be0f2297fac09d629d0ca3bebec76b9627\",\"length\":72}"

Prove that we are NOT caching errors (StatusCode == 200 only)

-------------------------------------------------------

`GetWithMeta("https://api.jcdecaux.com/vls/v3/contracts", 60)`:

-> first call:

"{\"cache\":\"MISS->CACHED\",\"key\":\"GET::https://api.jcdecaux.com/vls/v3/contracts?api" +
    "Key=13f2c4be0f2297fac09d629d0ca3bebec76b9627\",\"length\":2724}"

-> second call, after `\"ageSeconds\":12` 12 seconds (12 < 60 => HIT)

"{\"cache\":\"HIT\",\"key\":\"GET::https://api.jcdecaux.com/vls/v3/contracts?apiKey=13f2c" +
    "4be0f2297fac09d629d0ca3bebec76b9627\",\"ageSeconds\":12,\"length\":2724}"


### Postman response observation:
Route coordinates (Walk1Coords, BikeCoords, Walk2Coords, Segments.Coords) are always included because the web front-end uses them to draw the polyline on the map.
The Debug flag only adds developer-oriented information such as the top 3 candidate stations and resolved coordinates.



## Heavy SOAP client (Java) – `ProxyCacheJavaClient`

This module is a standalone Maven project that acts as a **heavy SOAP client** for the C# `ProxyCacheService`.  
The goal is to consume the WCF SOAP service from Java, without re-implementing any HTTP/caching logic on the Java side.

### How it works

- The C# service `ProxyCacheService` exposes SOAP operations such as:
  - `GetJcdecauxContractsGeneric(ttlSeconds)`
  - `GetJcdecauxStationsGeneric(contract, ttlSeconds)`
  - `Get(url)`, `GetWithTtl(url, ttlSeconds, forceRefresh, extendTtl)`
  - `GetWithMeta(url, ttlSeconds)`
  - `Evict(url)`
  - `Stats()`
- From Java we **import the WSDL** of this service and use the `jaxws-maven-plugin` to generate the client proxy classes under  
  `com.soap.proxycache.client.generated.*`.
- The class `TestProxyClient` shows how Java calls this SOAP service:

  1. **Generic JCDecaux cache**
     - `getJcdecauxContractsGeneric(3600)` and `getJcdecauxStationsGeneric("lyon", 30)`
     - These methods use the generic `GenericProxyCache` on the C# side and return the raw JSON payloads.

  2. **URL-based HTTP cache with HIT / MISS counters**
     - We pick a JCDecaux URL, e.g. `https://api.jcdecaux.com/vls/v3/contracts`.
     - First call: `getWithMeta(url, 30)`  
       → returns `{"cache":"MISS->CACHED", ...}` and `Stats()` shows `hits:0, misses:1`.
     - Second call with the same URL: `getWithMeta(url, 30)`  
       → returns `{"cache":"HIT", ...}` and `Stats()` shows `hits:1, misses:1`.
     - Then we call `evict(url)` to remove the entry from the cache.
     - Third call: `getWithMeta(url, 30)` again  
       → returns `{"cache":"MISS->CACHED", ...}` and `Stats()` shows `hits:1, misses:2`.

  This demonstrates that:
  - the C# `ProxyCacheService` is responsible for injecting the JCDecaux API key,
  - HTTP responses are cached in memory with a TTL,
  - cache hits and misses are correctly counted and exposed through the SOAP API,
  - the Java client only consumes the SOAP operations (heavy client generated from WSDL), without knowing any low-level HTTP details.

### How to run

1. Start the C# `ProxyCacheService` in Visual Studio (WCF SOAP service).
2. In IntelliJ, open the `ProxyCacheJavaClient` project.
3. Run the `TestProxyClient` main class.
4. The console output shows:
   - the raw JSON for JCDecaux contracts and stations (generic cache),
   - the cache behaviour for a specific URL (`MISS->CACHED`, `HIT`, `MISS->CACHED` after eviction),
   - the evolving `hits` / `misses` counters from `Stats()`.


## 🗺️ Displaying a World Map in Java (Bonus Feature)

To enhance the usability of the SOAP client, we implemented a **Java desktop map viewer**
based on **JXMapViewer2**, a Swing component rendering OpenStreetMap tiles.

Although the feature is optional in the course requirements, it provides **bonus points**
for a high-quality visual representation of itineraries.

### ✔ Implemented Functionality

* Integration with **JXMapViewer2** to display OpenStreetMap tiles inside a Swing window
* A simple UI allowing the user to input:

  * Origin address
  * Destination address
* The Java client invokes the **RoutingServiceREST** endpoint
  (`/itinerary`) to retrieve a full bike/walk itinerary
* The response contains detailed route geometry:

  * `Walk1Coords`
  * `BikeCoords`
  * `Walk2Coords`
  * Or full segment data in `Segments[]` with metadata
* The map dynamically renders:

  * **Walking segments** as gray **dashed** polylines
  * **Biking segments** as green **continuous** polylines
  * Start point marker (green)
  * Destination marker (red)
* **Adaptive zoom**:

  * Zooms in for short city routes
  * Zooms out for long routes (e.g., Paris → Lyon)

### 🖼️ Result

The final Java viewer displays the full itinerary on top of OpenStreetMap tiles,
with styling matching the main web frontend (walking vs biking), providing
a clean and intuitive visualization — exactly what the bonus specification suggests.

---

## Conclusion

The Heavy SOAP client and Java map viewer together form a complete, robust extension
to the core project.
The SOAP client demonstrates WSDL-based interoperability, while the map viewer provides
a polished visualization layer for the computed bike itineraries.

Both components exceed the expected requirements and showcase strong integration between
Java, SOAP, REST, and mapping technologies.





### Tests din afar frantei:
Aldi, Dublin -> Trinity College Dublin, Dublin 

Grand Place, Brussels, Belgium -> Parc du Cinquantenaire, Brussels, Belgium

Rue de l'Ecuyer 19, Bruxelles -> Galeries Royales Saint-Hubert, Bruxelles

Placa del Doctor Torrens, Valencia, Spain -> Centro de Especialidades El Grao, Valencia, Spain

Madrid -> Lyon ferry





### **Routing limitations of the GET Directions API**

This project uses the **OpenRouteService Directions API (GET)** to compute walking and cycling routes:

```
https://api.openrouteservice.org/v2/directions/{profile}?start={lon,lat}&end={lon,lat}
```

The GET endpoint **only supports start/end coordinates** and does **not accept advanced routing options**, such as:

* avoiding ferries
* custom route preferences
* restricting certain road types
* advanced routing features (`options.avoid_features`)

Because of this limitation, for **very long inter-city trips**, the default ORS engine may select a ferry route if it is considered the shortest option (e.g., Madrid → Lyon).


To gain full control over the routing behavior, the project can later switch to the **POST** Directions API.
The POST version supports JSON request bodies and allows configuring advanced options like:

```json
{
  "coordinates": [[lon, lat], [lon, lat]],
  "preference": "fastest",
  "options": {
    "avoid_features": ["ferries"]
  }
}
```



## Real-Time Notifications (ActiveMQ + STOMP, SOAP Integration, JCDecaux Data)

This feature adds live external-style alerts (weather, pollution, bike availability) to the front-end while the user is viewing an itinerary. It combines:

- a standalone **C# notification producer**,  
- SOAP calls to the **ProxyCacheService**,  
- real station data from the **JCDecaux bikes REST API**,  
- **ActiveMQ topics**,  
- and a **JavaScript STOMP client** in the browser.

---

### 1. NotificationService (C# Producer with Real JCDecaux Data)

`NotificationService` is a separate console application that periodically sends three event types:

- **meteo** – short-term weather alerts  
- **pollution** – air quality alerts  
- **bikes** – real JCDecaux bike availability alerts  

For **BIKES**, the system does *not* use fake numbers. Instead, it:

1. Calls the **ProxyCacheService** via SOAP (`ProxyServiceClient` autogenerated from the WSDL).
2. Invokes `GetJcdecauxStationsGeneric(contract, ttl)` to fetch and cache JCDecaux station data.
3. Parses the JSON station list.
4. Randomly picks one JCDecaux station.
5. Extracts:
   - number of available bikes
   - number of available stands
   - the station name
   - **real station coordinates** (`lat`, `lng`)
6. Determines severity (`info`, `warning`, `danger`) based on availability.
7. Sends a JSON event to `/topic/bikes`, including:
   - station name
   - bikes / stands
   - severity level
   - **lat/lon** coordinates for map display
   - timestamp

All events are published to ActiveMQ:

- `/topic/meteo`
- `/topic/pollution`
- `/topic/bikes`

#### JCDecaux contract is configurable

The JCDecaux city used for alerts is set in `NotificationService`’s `App.config`:

```xml
<appSettings>
    <add key="ALERT_CONTRACT" value="toulouse" />
</appSettings>
````

Changing this value (e.g., `lyon`, `bruxelles`) instantly changes the city whose bike alerts are monitored — without touching the code.

---

### 2. ActiveMQ (Message Broker)

ActiveMQ is used as the transport layer for all notifications.
Messages are sent as JSON over **STOMP via WebSocket**:

```
ws://localhost:61614/stomp
```

* `NotificationService` → publishes messages
* the Front-End → subscribes in real time

---

### 3. Web Front-End (JavaScript STOMP Client + Leaflet Map)

The browser connects to ActiveMQ using a JavaScript STOMP client.

The UI lets the user enable/disable subscriptions to:

* Meteo
* Pollution
* Bikes

Incoming events appear instantly in the **“Real-time events”** list, with severity-based colors:

* **blue** → info
* **yellow** → warning
* **red** → danger

Each event is also shown on the map:

#### • METEO

Displayed near the **route origin**.

#### • POLLUTION

Displayed near the **route destination**.

#### • BIKES

If the event contains `Lat`/`Lon` (real JCDecaux station coordinates),
the marker is placed **exactly at that station**.

If coordinates are missing (edge cases), the system falls back to:

* bike pickup station,
* or bike dropoff station,
* or midpoint between origin and destination.

Clicking any alert in the list recenters the map on the corresponding marker.

---

### Summary

* **NotificationService** produces real-time meteo/pollution alerts and *real JCDecaux bike availability alerts*.
* **ProxyCacheService** provides JCDecaux data over SOAP, with caching.
* **ActiveMQ** distributes events using STOMP/WebSocket.
* **Front-end**:

  * subscribes to selected topics,
  * displays colored notifications,
  * and places alert markers on the map based on the current itinerary or real station coordinates.
* The monitored **JCDecaux contract is configurable** in `NotificationService` (`ALERT_CONTRACT`), making the system reusable for any city.

* Real-time alerts are *started manually* from the front-end via a **“Start alerts” button**, which connects to ActiveMQ over STOMP and begins receiving events only when the user is ready.
