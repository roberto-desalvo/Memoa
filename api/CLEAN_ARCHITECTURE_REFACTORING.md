# Refactoring Clean Architecture - Riepilogo Modifiche

## Data: 12 Gennaio 2026

## Obiettivo
Ristrutturare il progetto Memoa per aderire ai principi di Clean Architecture, correggendo violazioni critiche e migliorando la manutenibilità del codice.

---

## Modifiche Implementate

### 1. ✅ Creato Layer Infrastructure (Memoa.Infrastructure)

**Problema**: Il layer Domain conteneva implementazioni infrastrutturali (DbContext, repository concreti, Supabase service), violando la Dependency Inversion Principle.

**Soluzione**: Creato nuovo progetto `Memoa.Infrastructure` contenente:
- **Data/MemoaDbContext.cs**: DbContext spostato da Domain
- **Migrations/**: Tutte le migration EF Core con namespace aggiornati
- **Repositories/EntryRepository.cs**: Implementazione concreta del repository
- **Repositories/UnitOfWork.cs**: Implementazione del pattern Unit of Work
- **Services/SupabaseAudioStorageService.cs**: Servizio storage audio
- **Services/CurrentUserService.cs**: Implementazione del servizio utente corrente
- **InfrastructureServiceExtensions.cs**: Extension method per DI

**Dipendenze Infrastructure**:
```xml
- Memoa.Domain (solo interfacce)
- Microsoft.EntityFrameworkCore
- Npgsql.EntityFrameworkCore.PostgreSQL
- Microsoft.AspNetCore.Http.Abstractions
```

---

### 2. ✅ Pulito Layer Domain (Memoa.Domain)

**Problema**: Domain conteneva dipendenze a framework esterni (MediatR, EF Core).

**Soluzione**:
- ❌ **Rimosso**: MediatR, EntityFrameworkCore, Npgsql
- ❌ **Rimosso**: Implementazioni concrete (MemoaDbContext, EntryRepository, RepositoryBase)
- ❌ **Rimosso**: Request MediatR
- ✅ **Mantenuto**: Solo entità, interfacce repository, DTOs, Result types

**Domain ora contiene**:
- `Entities/Entry.cs` (arricchita con logica di dominio)
- `Repositories/IEntryRepository.cs` (solo interfaccia)
- `Repositories/IUnitOfWork.cs` (nuova interfaccia)
- `Dtos/` (oggetti di trasferimento dati)

**Dipendenze Domain** (pulite):
```xml
- FluentResults (per Result pattern)
```

---

### 3. ✅ Arricchito Domain Model

**Problema**: Entry era un modello anemico (solo getter/setter) senza comportamento.

**Soluzione**: Trasformato `Entry` in un rich domain model con:

**Factory Method**:
```csharp
Entry.Create(id, userId, text, createdAt, ...)
```

**Business Methods**:
- `UpdateText(newText, audioTranscription)`: Valida e aggiorna testo
- `ReplaceAudio(...)`: Gestisce sostituzione audio con validazioni
- `RemoveAudio()`: Rimuove audio dall'entry
- `BelongsToUser(userId)`: Verifica ownership
- `HasAudio()`: Check presenza audio

**Validazioni integrate**:
- Testo obbligatorio e max 100.000 caratteri
- Audio MIME obbligatorio se c'è audio
- Setter privati per incapsulamento

**Prima**:
```csharp
public class Entry
{
    public Guid Id { get; set; }
    public string Text { get; set; }
    // ...
}
```

**Dopo**:
```csharp
public class Entry
{
    public Guid Id { get; private set; }
    public string Text { get; private set; }
    
    public static Result<Entry> Create(...) { }
    public Result UpdateText(...) { }
    public bool BelongsToUser(string userId) { }
}
```

---

### 4. ✅ Implementato Unit of Work Pattern

**Problema**: Repository esponeva `SaveChangesAsync()`, violando Single Responsibility.

**Soluzione**:
- Creato `IUnitOfWork` in Domain (interfaccia)
- Implementato `UnitOfWork` in Infrastructure
- Rimosso `SaveChangesAsync()` da `IEntryRepository`

**Benefici**:
- Separazione di responsabilità
- Migliore gestione transazioni
- Supporto per transazioni esplicite (Begin, Commit, Rollback)

---

### 5. ✅ Disaccoppiato da HttpContext

**Problema**: Gli handler accedevano direttamente a `IHttpContextAccessor`, accoppiando Application layer all'infrastruttura web.

**Soluzione**:
- Creato `ICurrentUserService` in Application (interfaccia)
- Implementato `CurrentUserService` in Infrastructure
- Aggiornati tutti gli handler per usare l'astrazione

**Prima**:
```csharp
var user = _accessor.HttpContext.User;
var userId = user.FindFirst("sub")?.Value ?? ...;
```

**Dopo**:
```csharp
var userId = _currentUserService.GetCurrentUserId();
```

---

### 6. ✅ Spostato Request MediatR da Domain ad Application

**Problema**: MediatR requests (use cases) erano nel Domain layer.

**Soluzione**:
- Spostati tutti i `*Request.cs` da `Memoa.Domain.Requests` a `Memoa.Application.Requests`
- Aggiornati namespace in handler e controller
- Domain ora non ha dipendenze da MediatR

**File spostati**:
- CreateEntryRequest.cs
- UpdateEntryRequest.cs
- DeleteEntryRequest.cs
- GetEntriesRequest.cs
- GetEntryByIdRequest.cs
- GetUserRequest.cs

---

### 7. ✅ Implementato Mapping Layer

**Problema**: Mapping manuale ripetuto in ogni handler.

**Soluzione**: Creato `EntryMappingExtensions.cs` con extension methods:
```csharp
entry.ToDto()
entries.ToDtoList()
```

**Benefici**:
- DRY (Don't Repeat Yourself)
- Centralizzazione logica di mapping
- Facilità di manutenzione

---

### 8. ✅ Aggiornati tutti gli Handler

**Modifiche applicate ai 6 handler**:
1. ❌ Rimosso `IHttpContextAccessor`
2. ✅ Aggiunto `ICurrentUserService`
3. ✅ Aggiunto `IUnitOfWork`
4. ✅ Usati domain methods (Create, UpdateText, ReplaceAudio, BelongsToUser)
5. ✅ Usati mapping extensions (.ToDto())
6. ✅ Namespace aggiornati (Application.Requests)

**Handler aggiornati**:
- CreateEntryRequestHandler
- UpdateEntryRequestHandler
- DeleteEntryRequestHandler
- GetEntriesRequestHandler
- GetEntryByIdRequestHandler
- GetUserRequestHandler

---

### 9. ✅ Ristrutturato Dependency Injection

**Prima** (Program.cs disorganizzato):
```csharp
builder.Services.AddDbContext<MemoaDbContext>(...);
builder.Services.AddScoped<IEntryRepository, EntryRepository>();
builder.Services.AddHttpClient();
builder.Services.Configure<SupabaseAudioStorageOptions>(...);
// ...
```

**Dopo** (pulito e organizzato):
```csharp
builder.Services.AddApplication();         // MediatR, handlers
builder.Services.AddInfrastructure(config); // DbContext, repos, services
```

**Extension methods creati**:
- `ApplicationServiceExtensions.AddApplication()`
- `InfrastructureServiceExtensions.AddInfrastructure()`

---

### 10. ✅ Aggiornate Project References

**Memoa.WebApi.csproj**:
```xml
<ProjectReference Include="Memoa.Application" />
<ProjectReference Include="Memoa.Infrastructure" /> <!-- NUOVA -->
<ProjectReference Include="Memoa.Core" />
<ProjectReference Include="Memoa.Domain" />
```

**Memoa.Application.csproj**:
```xml
- Rimosso: dipendenze ASP.NET e EF Core non necessarie
+ Mantenuto: MediatR, FluentResults
```

**Memoa.Infrastructure.csproj** (nuovo):
```xml
<ProjectReference Include="Memoa.Domain" />
+ EF Core, Npgsql, HttpClient
```

---

## Struttura Finale dei Layer

```
┌─────────────────────────────────────────────────┐
│          Memoa.WebApi (Presentation)            │
│  - Controllers                                  │
│  - Program.cs (DI configuration)                │
└────────────┬────────────────────────────────────┘
             │ dipende da
             ▼
┌────────────────────────────────────────────────┐
│      Memoa.Application (Use Cases)             │
│  - Handlers (MediatR)                          │
│  - Requests (Commands/Queries)                 │
│  - Services Interfaces (ICurrentUserService)   │
│  - Mapping Extensions                          │
└────────────┬───────────────────────────────────┘
             │ dipende da
             ▼
┌────────────────────────────────────────────────┐
│       Memoa.Domain (Business Logic)            │
│  - Entities (Entry con logica)                 │
│  - Repository Interfaces                       │
│  - IUnitOfWork                                 │
│  - DTOs                                        │
└────────────────────────────────────────────────┘
             ▲
             │ implementa
             │
┌────────────┴───────────────────────────────────┐
│    Memoa.Infrastructure (Implementation)       │
│  - MemoaDbContext                              │
│  - Repositories Implementation                 │
│  - UnitOfWork Implementation                   │
│  - CurrentUserService                          │
│  - SupabaseAudioStorageService                 │
│  - EF Core Migrations                          │
└────────────────────────────────────────────────┘
```

---

## Principi Clean Architecture Rispettati

### ✅ Dependency Inversion Principle
- Domain **non dipende** da nessuno
- Infrastructure dipende **solo** da Domain (interfacce)
- Flusso delle dipendenze: Presentation → Application → Domain ← Infrastructure

### ✅ Separation of Concerns
- **Domain**: Business logic pura
- **Application**: Orchestrazione use cases
- **Infrastructure**: Dettagli implementativi
- **Presentation**: UI/API

### ✅ Single Responsibility Principle
- Repository: solo accesso dati
- UnitOfWork: solo gestione transazioni
- Handlers: solo orchestrazione
- Domain entities: solo logica di business

### ✅ Rich Domain Model
- Entry non più anemic
- Validazioni nel domain
- Factory methods
- Behavior encapsulation

---

## Violazioni Risolte

### 🔴 CRITICHE (Risolte)
1. ✅ Domain layer con dipendenze EF Core e MediatR → **RIMOSSO**
2. ✅ MemoaDbContext nel Domain → **SPOSTATO in Infrastructure**
3. ✅ Repository implementations nel Domain → **SPOSTATE in Infrastructure**
4. ✅ HttpContext nei handler → **SOSTITUITO con ICurrentUserService**

### 🟡 MAJOR (Risolte)
5. ✅ Anemic domain model → **ARRICCHITO con behavior**
6. ✅ SaveChangesAsync nel repository → **ESTRATTO in UnitOfWork**
7. ✅ Mapping manuale ripetuto → **CENTRALIZZATO in extensions**
8. ✅ Business logic negli handler → **SPOSTATA nel domain**

---

## Prossimi Step Consigliati (Non Implementati)

### 11. 🔲 FluentValidation
Aggiungere validazioni con FluentValidation per le request:
```csharp
public class CreateEntryRequestValidator : AbstractValidator<CreateEntryRequest>
{
    public CreateEntryRequestValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(100000);
        // ...
    }
}
```

### 12. 🔲 Authorization Policies
Centralizzare autorizzazione:
```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("EntryOwner", policy => 
        policy.Requirements.Add(new EntryOwnerRequirement()));
});
```

### 13. 🔲 Domain Events
Implementare eventi per logica cross-aggregate:
```csharp
public class EntryDeletedEvent : IDomainEvent
{
    public Guid EntryId { get; }
    public string AudioPath { get; }
}
```

### 14. 🔲 CQRS Separation
Separare modelli di lettura e scrittura:
```csharp
// Query-specific DTOs
public class EntryListItemDto { }
public class EntryDetailDto { }
```

---

## Testing

### Test Suggeriti da Implementare

1. **Unit Tests Domain**:
   - Entry.Create validations
   - Entry.UpdateText
   - Entry.ReplaceAudio

2. **Unit Tests Handlers**:
   - Mock ICurrentUserService
   - Mock IUnitOfWork
   - Verificare logica orchestrazione

3. **Integration Tests**:
   - Test con DbContext in-memory
   - Verificare migrations

---

## Note di Migrazione

### ⚠️ Breaking Changes
1. Namespace cambiati per Request (Domain → Application)
2. SaveChangesAsync ora su IUnitOfWork
3. Entry con costruttore privato (usare factory Create)

### ✅ Backward Compatibility
- API endpoints invariati
- Database schema invariato
- Migrations preservate

---

## Performance & Sicurezza

### Miglioramenti Performance
- UnitOfWork permette batch operations
- Transazioni esplicite per operazioni multiple

### Miglioramenti Sicurezza
- Validazioni centralizzate nel domain
- Ownership check con domain methods
- Private setters prevengono modifiche non autorizzate

---

## Conclusioni

Il refactoring ha trasformato un'architettura con violazioni critiche di Clean Architecture in una soluzione ben strutturata che:

1. ✅ **Rispetta la Dependency Rule**
2. ✅ **Separa le responsabilità**
3. ✅ **Ha un Domain ricco e testabile**
4. ✅ **È facilmente estendibile**
5. ✅ **Riduce l'accoppiamento**

Il codice è ora più **manutenibile**, **testabile** e **scalabile**.
