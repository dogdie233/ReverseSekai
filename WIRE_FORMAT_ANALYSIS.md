# Project SEKAI Wire Format Analysis - Complete Reference

## Overview

This document provides a comprehensive analysis of the wire format used in Project SEKAI's multiplayer systems, based on analysis of the SekaiApiModel.Jp project. All serialization uses MessagePack v3.1.4.

## Core Architecture

### 1. Serialization Foundation

- **MessagePack v3.1.4** - Binary serialization format
- **String keys**: [Key("FieldName")] - for debugging
- **Numeric keys**: [Key(0)], [Key(1)] - for efficiency (RankLive)

### 2. DynamicPropertyPayload

The universal flexible property container:
- `isRSend`: byte (reliability flag)
- `values`: Dictionary<int, byte[]> (generic property store)

Used throughout the system for extensibility.

### 3. Message Wrappers

**MessagePacketData** (room-level):
- Key 0: uint id (sequence)
- Key 1: string uid (user ID)
- Key 2: object payload

**GroupMessagePacketData** (group-level):
- Key 0: uint id
- Key 1: string uid
- Key 2: string groupId
- Key 3: object payload

## Room Synchronization

### 4. Room Sync Data

**RoomSyncData** (full state):
- IsJoin: bool
- RoomCreateTime: uint
- RoomID: string
- OwnerID: string
- RoomProperty: DynamicPropertyPayload
- Players: RoomSyncPlayer[]
- NetworkObjects: NetworkObject[]

**RoomSyncDataMinimal** (bandwidth-optimized):
- MySyncPlayer: RoomSyncPlayer (current player only)
- UserIDs: string[] (all participants)

**RoomSyncPlayerMaximal** (enhanced):
- IsOwner: bool
- NetworkObjects: NetworkObject[] (player-specific)

### 5. 3D Transforms

**Vec3D**: X, Y, Z (floats)
**Vec4D**: X, Y, Z, W (floats - quaternion)

**SyncTransformData**:
- ViewID, Flags
- Position: Vec3D
- Rotate: Vec4D (quaternion)
- Scale: Vec3D
- Speed: Vec3D
- TurnSpeed: float
- Behavior: byte

**NetworkObject**:
- ViewID, Type, ObjectType, Prefab
- ObjectData: byte[]
- Position, Rotate, Scale
- OwnerUserID: string

## Room Creation and Joining

### 6. Room Creation

**RoomCreateOption**:
- MaxMembers: int
- AllowEmpty: bool
- JoinRoom: bool
- RoomTTL: uint
- Interval: uint
- IsPrivate: bool

**RoomInitialData**:
- CreateOption, RoomProperty, PlayerProperty

### 7. Joining Rooms

**JoinRoomPayload**:
- RoomCreateTime, RoomID
- IsJoined: bool
- OwnerID: string
- RoomProperty, PlayerProperty
- UserIDs: string[]
- PrivateRoomNumber: int? (for private rooms)

## Matchmaking System

### 8. Matchmaking Conditions

**SearchMatchmakeConditionPayload**:
- MatchingName: string
- SearchProp: Dictionary<string, int>
- IsScaleUp: bool

**EntryMatchmakeConditionPayload**:
- SearchProps: List<Dictionary<string, int>>
- MatchingTTL: uint
- AutoRefresh: bool

### 9. Search/Join/Create

**SearchJoinOrCreateData**:
- MatchingName, SearchProps
- RoomProperty, PlayerProperty

**SearchJoinData**:
- IsCreate: bool
- EntryCondition, SearchCondition
- InitialData, Retry: int

### 10. Groups

**GroupCreateOption**:
- AllowEmpty, GroupTTL, MatchingTTL, Interval

**GroupMatchmakeEntryCondition**:
- MatchingName, MatchingTTL, SearchProps

**AddMatchingForGroupPayload**:
- GroupID, EntryCondition

**JoinGroupPayload**:
- GroupID, MatchingName, SearchProps

## Live Event Types

### 11. MultiLive (4-player Cooperative)

**MultiLiveRoomSyncData**:
- UserID, RoomCreateTime, Index
- RoomID, OwnerID
- RoomProperty, PlayerProperty, Players
- PrivateRoomNumber: int?

**MultiLiveSearchJoinOrCreateData**:
- Mode: int
- TotalPower: int

**MultiLiveUnLockJoinData**:
- RoomID, PrivateRoomNumber
- TotalPowerUpperLimit, TotalPowerLowerLimit
- TotalPower, PlayerProperty

**CustomRoomSettingData**:
- ScoreCalculateType: enum
- MusicSelectionType: enum
- MusicDifficultyTypes: array
- IsDisplayPlayerInfo: bool

### 12. CheerfulLive (Team PvP)

**CheerfulRoomSyncData**:
- Same structure as MultiLiveRoomSyncData

**CheerfulLiveSearchJoinOrCreateData**:
- Mode, TotalPower
- OwnTeamID: int (team differentiation)

### 13. RankLive (Ranked)

Uses numeric keys for efficiency.

**RankLivePlayerInfoPayload**:
- Key 0: long userId
- Key 1: string userName
- Key 2: int cardId
- Key 3: bool doneSpecialTraining
- Key 4: int consecutiveWinCount
- Key 5: int tier
- Key 6: int masterLv
- Key 7: int cardLv
- Key 8: bool isPlacement

**JoinRoomPayloadV2**:
- RoomID, RoomCreateTime
- IsJoined, IsCreated, IsMatchingEntry
- RoomSyncData: full structure embedded

## Authentication

### 14. Connection Setup

**UserDiarkisAuthResponse**:
- userId: long
- clientKey, tcpHost, tcpPort, udpHost, udpPort
- sid: string (session ID)
- encryptionKey, encryptionIv, encryptionMacKey

Connection Flow:
1. Get auth token from main API
2. Call UserDiarkisAuth endpoint
3. Connect to tcpHost:tcpPort
4. Optional UDP connection
5. Use encryption keys for message encryption

## Enums

- **ScoreCalculateType**: normal=0, competitive=1
- **MusicSelectionType**: each=0, host=1, random=2
- **MusicDifficulty**: none=0, easy=1, normal=2, hard=3, expert=4, master=5, append=6

## Implementation Strategy

1. **Serialization**: MessagePack v3.1.4 with string/numeric key support
2. **Property System**: DynamicPropertyPayload for extensibility
3. **Room Lifecycle**: Create -> Update -> Join -> Sync -> Leave
4. **Matchmaking**: SearchProps filtering, scale-up logic, group coordination
5. **Private Rooms**: PrivateRoomNumber-based access control
6. **Live Types**: MultiLive (4p coop), CheerfulLive (team PvP), RankLive (ranked)
7. **Network Sync**: NetworkObject synchronization with quaternion rotation
8. **Bandwidth**: Minimal variants for frequent updates, numeric keys for RankLive

