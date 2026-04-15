# SelfHostSekai Multiplayer System

## Overview

This document describes the multiplayer infrastructure for Project SEKAI implemented using the Diarkis real-time networking framework. The system enables local/self-hosted multiplayer gameplay without depending on official Bandai Namco servers.

## Architecture

### Wire Format Foundation

All multiplayer communication uses **MessagePack v3.1.4** binary serialization with the following structure:

MessagePacketData:
  - id: uint (sequence number)
  - uid: string (user ID)
  - payload: object (type-specific data)

GroupMessagePacketData:
  - id: uint
  - uid: string
  - groupId: string
  - payload: object

Reference: WIRE_FORMAT_ANALYSIS.md contains complete specifications for all message types.

### Core Services

#### 1. RoomService (IRoomService)

Manages room lifecycle and player membership.

Key Methods:
- CreateRoomAsync: Initializes new room with owner as first player
- GetRoomAsync: Retrieves room from cache with expiration checking
- JoinRoomAsync: Adds player to room, validates capacity and power limits
- LeaveRoomAsync: Removes player, handles owner reassignment
- GetRoomStateAsync: Returns full room state (RoomSyncData)
- GetRoomStateMinimalAsync: Returns minimal state (current player + user list only)
- UpdateRoomPropertyAsync/UpdatePlayerPropertyAsync: Updates dynamic properties
- ValidatePrivateRoomAccessAsync: Checks private room PIN
- ValidatePowerLimitsAsync: Enforces power ceiling/floor
- GetAvailableRoomsAsync: Enumerates joinable non-full rooms

Storage:
- In-memory cache (IMemoryCache) with TTL-based expiration
- Keys: "room:" + roomId
- Future: Redis for distributed deployments

Room Model:
- RoomID: Unique GUID
- OwnerID: Owner user ID
- RoomCreateTime: Unix timestamp
- Players: Dictionary of current members
- RoomProperty: Flexible room properties (DynamicPropertyPayload)
- NetworkObjects: Synchronized objects in scene
- TTL: Time-to-live in seconds
- MaxMembers: Capacity (default 4)
- IsPrivate: Private vs public
- PrivateRoomNumber: PIN for private rooms
- TotalPowerUpperLimit/LowerLimit: Power constraints
- CreatedAt/ExpiresAt: Timestamps
- AllowEmpty: Persist when empty
- LiveType: "MultiLive", "CheerfulLive", "RankLive"

#### 2. MatchmakingService (IMatchmakingService)

Implements SearchProps-based room discovery and join-or-create logic.

Key Methods:
- SearchRoomsAsync: Filters available rooms matching SearchProps criteria
- SearchJoinOrCreateAsync: Searches for room, joins if found, creates new if not
- ApplyScaleUp: Expands search tolerance on iteration
- ValidateSearchProps: Validates player properties against search criteria
- ConvertProperties: Converts DynamicPropertyPayload to Dictionary<string, int>

Search Matching Algorithm:
tolerance = searchValue * 0.1
if (Math.Abs(playerValue - searchValue) <= tolerance)
    return true;  Match found

On retry, values are scaled down to expand search.

#### 3. DiarkisService (IDiarkisService) - PLANNED Phase 2

Will provide TCP/UDP connectivity to Diarkis backend.

Planned Methods:
- ConnectAsync(host, port, sessionId, encryptionKey)
- DisconnectAsync()
- IsConnected property
- SendMessageAsync(byte[] data)
- RegisterMessageHandler(handler)
- UnregisterMessageHandler()
- Heartbeat mechanism every 30 seconds

## Configuration

### DiarkisOptions (appsettings.json)

Host: "localhost"
Port: 8000
UdpPort: 8001
EncryptionAlgorithm: "AES-256-GCM"

### Dependency Injection (Program.cs)

Services are registered in DI container:
- IRoomService -> RoomService (scoped)
- IMatchmakingService -> MatchmakingService (planned)
- IMemoryCache for room storage

## Endpoints

### GET /api/user/{userId}/user_diarkis_auth

Authenticates user for Diarkis connectivity.

Returns:
- userId, clientKey
- tcpHost, tcpPort, udpHost, udpPort
- sid (session ID)
- encryptionKey, encryptionIv, encryptionMacKey (for AES-256-GCM)

## Live Types

The system supports three multiplayer live types:

### 1. MultiLive (4-player Cooperative)
- 4 players cooperatively playing the same song
- Score calculated individually or competitively
- Music selection by host or random
- Difficulty locked or individual choice

### 2. CheerfulLive (Team PvP)
- 2v2 team-based competition
- Separate team scores
- Team-based power limits
- Leaderboard tracking per team

### 3. RankLive (Ranked Competition)
- Ranked ladder matches
- Individual player ratings
- Placement matches for new players
- Uses numeric MessagePack keys for efficiency

## Bandwidth Optimization

### Minimal State Updates
Instead of sending full RoomSyncData on every update:
- Send RoomSyncDataMinimal containing only:
  - Current player's full state
  - List of all user IDs
- Reduces bandwidth by ~80% for frequent updates

### MessagePack Efficiency
- String keys for debugging
- Numeric keys for efficiency (RankLive)
- Binary serialization (vs JSON) reduces payload size by ~50%

## Property System (DynamicPropertyPayload)

Generic extensible property container:
- isRSend: byte (reliability flag)
- values: Dictionary<int, byte[]> (property storage)

Allows properties without schema changes:
- Room properties: matchmaking criteria, selected song, difficulty
- Player properties: character selection, card level, team ID

## Synchronization Strategy

### 1. Initial Join (Full State)
Client receives RoomSyncData with:
- All players with complete state
- All network objects in scene
- Room configuration
- Owner information

### 2. Ongoing Updates (Minimal State)
Client receives RoomSyncDataMinimal with:
- Only self and user list
- Changes sent via delta updates
- NetworkObject position/rotation updates (quaternion-based)

### 3. 3D Transform Sync (NetworkObject)
- ViewID, Flags
- Position: Vec3D (X, Y, Z floats)
- Rotate: Vec4D (X, Y, Z, W quaternion)
- Scale: Vec3D
- Speed: Vec3D
- TurnSpeed: float
- Behavior: byte

## Technical Decisions

| Decision | Rationale |
|----------|-----------|
| In-Memory Cache | Simple MVP. Redis migration planned. |
| 10% Search Tolerance | Balanced matching for reliability. |
| Minimal State Updates | 80% bandwidth reduction for active rooms. |
| Private Room PIN | Stateless access control. |
| MessagePack | Binary efficiency, wire format compatibility. |
| Owner Reassignment | Prevents orphaned rooms when owner leaves. |

## Performance Targets

- Connection establishment: < 100ms
- Message round-trip (TCP): < 50ms
- Message round-trip (UDP): < 20ms
- Memory per active room: < 10MB
- CPU overhead per connection: < 1% at 60fps

## Project Status

Phase 1: Foundation - COMPLETE
- RoomService implementation
- MatchmakingService implementation
- DiarkisAuthController endpoint
- Configuration and DI setup
- Zero compilation errors

Phase 2: Diarkis Integration - PENDING
- DiarkisService TCP/UDP implementation
- Message routing framework
- Encryption layer
- Heartbeat mechanism

Phase 3: Live Type Support - PENDING
Phase 4: Advanced Features - PENDING
Phase 5: Testing & Polish - PENDING

## References

- WIRE_FORMAT_ANALYSIS.md - Complete wire format specification
- PHASE_2_ROADMAP.md - Detailed Phase 2 implementation roadmap
- CURRENT_PROGRESS.md - Overall project progress and milestones
