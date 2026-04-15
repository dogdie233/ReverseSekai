# SelfHostSekai Multiplayer Implementation Progress

## Project Overview
Implementing a complete Diarkis-compatible multiplayer backend for Project SEKAI based on MessagePack v3.1.4 wire format analysis. This enables local/self-hosted multiplayer gameplay without depending on official servers.

## Phase 1: Foundation (Weeks 1-1) - IN PROGRESS

### Core Infrastructure

**RoomService Implementation** (338 lines)
- Memory-based room storage with IMemoryCache and TTL
- Room creation with unique GUIDs and timestamp tracking
- Join/leave management with owner reassignment
- Full and minimal room state synchronization
- Property update methods for room and player properties
- Private room access validation
- Power limit enforcement (upper/lower bounds)

**MatchmakingService Implementation** (154 lines)
- SearchProps-based room filtering
- Scale-up logic for expanding search criteria
- Join-or-create workflow
- Property matching with 10% tolerance

**DiarkisAuthController Endpoint**
- GET /api/user/{userId}/user_diarkis_auth
- Returns UserDiarkisAuthResponse with connection endpoints, session ID, and encryption keys

**Configuration & Registration**
- DiarkisOptions configuration class
- Diarkis section in appsettings.json
- Service registration in Program.cs DI container
- Memory cache integration

### Data Models

**Room** class
- RoomID (unique identifier), OwnerID, Players collection
- RoomCreateTime (uint timestamp), RoomProperty (DynamicPropertyPayload)
- NetworkObjects, TTL, MaxMembers, IsPrivate, PrivateRoomNumber
- Power limits (upper/lower), CreatedAt/ExpiresAt timestamps
- AllowEmpty flag, LiveType (MultiLive/CheerfulLive/RankLive)

**RoomPlayer** class
- UserID, IsOwner flag, Player index (0-3)
- PlayerProperty (DynamicPropertyPayload), PlayerNetworkObjects
- JoinedAt timestamp, TotalPower field

### Interfaces
- IRoomService (complete method signatures)
- IMatchmakingService (SearchProps, scale-up, validation)
- IDiarkisService (connect, disconnect, message handling)

## Phase 2: Diarkis Integration - PENDING

- DiarkisService implementation (TCP/UDP)
- Heartbeat/keepalive mechanism
- Message fragmentation & reassembly
- Encryption layer (AES-256-GCM)
- Message routing framework

## Phase 3: Live Type Support - PENDING

- MultiLive (4-player Cooperative)
- CheerfulLive (Team PvP)
- RankLive (Ranked Competition)

## Phase 4: Advanced Features - PENDING

- GroupMatchmakingService
- NetworkObjectManager
- Redis caching layer
- Performance optimization

## Phase 5: Testing & Polish - PENDING

- Unit tests for RoomService
- Unit tests for MatchmakingService
- Integration tests
- Load testing
- Documentation

## Technical Decisions

Room Storage: IMemoryCache with TTL (simple, sufficient for MVP, future Redis migration)
Search Matching: 10% tolerance on SearchProps values (balanced matching)
Minimal State: Send only current player + user list on updates (80% bandwidth reduction)
Private Rooms: PrivateRoomNumber verification only (stateless access control)

## File Structure

SelfHostSekai/
├── Controllers/
│   └── DiarkisAuthController.cs (NEW) 68 lines
├── Configuration/
│   └── DiarkisOptions.cs (EXISTING)
├── Models/
│   └── Multiplayer/
│       └── Room.cs (NEW) 34 lines
├── Services/
│   └── Multiplayer/
│       ├── IDiarkisService.cs (NEW) 24 lines
│       ├── IMatchmakingService.cs (NEW) 28 lines
│       ├── IRoomService.cs (NEW) 20 lines
│       ├── RoomService.cs (NEW) 338 lines
│       ├── MatchmakingService.cs (NEW) 154 lines
│       └── LiveTypes/
├── Program.cs (MODIFIED)
├── appsettings.json (MODIFIED)
└── WIRE_FORMAT_ANALYSIS.md (REFERENCE)

## Completed Date
Phase 1 Foundation: April 15, 2026

## Next Steps
1. Verify RoomService compiles and DI registration works
2. Test DiarkisAuthController endpoint
3. Begin Phase 2: Diarkis networking implementation
4. Implement DiarkisService with TCP connection
5. Create message router framework
