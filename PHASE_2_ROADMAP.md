# Phase 2: Diarkis Integration - Detailed Roadmap

## Phase 2 Overview
Implement the DiarkisService to provide actual TCP/UDP connectivity to the Diarkis real-time networking backend, enabling live room synchronization and player communication.

## 2.1 DiarkisService Core Implementation

### Objectives:
- TCP connection management with Diarkis server
- UDP connection for low-latency game state sync
- Session-based authentication using encryption keys
- Message serialization/deserialization with MessagePack
- Error handling and reconnection logic

### Implementation Plan:

**Create: DiarkisService.cs**
```
Location: Services/Multiplayer/DiarkisService.cs
Size Estimate: 300-400 lines
Dependencies:
  - IDiarkisService (interface exists)
  - DiarkisOptions (configuration exists)
  - MessagePackSerializerContext (for MessagePack)
  - System.Net.Sockets (TCP/UDP)
```

Key Methods:
- ConnectAsync(string host, int port, string sessionId, byte[] encryptionKey)
- DisconnectAsync()
- IsConnected property
- SendMessageAsync(byte[] data)
- RegisterMessageHandler(Func<byte[], Task> handler)
- UnregisterMessageHandler()
- Heartbeat mechanism (every 30 seconds)

### Wire Format Details:
Per WIRE_FORMAT_ANALYSIS.md:
- MessagePacketData: id (seq), uid (userId), payload (object)
- GroupMessagePacketData: id, uid, groupId, payload
- All payloads are MessagePack serialized objects

## 2.2 Message Routing Framework

### Objectives:
- Route incoming messages to appropriate handlers
- Support multiple message types (Room creation, player join, property updates, etc.)
- Enable async/await message processing

### Implementation Plan:

**Create: MessageRouter.cs**
```
Location: Services/Multiplayer/MessageRouter.cs
Size Estimate: 200 lines
Dependencies:
  - IDiarkisService
  - IRoomService
  - IMatchmakingService
```

Message Types to Route:
- RoomCreateResponse
- JoinRoomResponse
- RoomSyncDataUpdate
- PlayerPropertyUpdate
- NetworkObjectUpdate
- MatchmakingUpdate

### Message Handler Pattern:
```csharp
public delegate Task<IMessageHandler> MessageHandlerFactory(string messageType);
public interface IMessageHandler
{
    Task<object?> HandleAsync(object payload);
}
```

## 2.3 Encryption Layer

### Objectives:
- Apply AES-256-GCM encryption to outgoing messages
- Decrypt incoming messages
- Validate MAC tags for integrity

### Key Points:
- Encryption keys provided by UserDiarkisAuthResponse
- Each message includes MAC for integrity verification
- IV may be needed per message or shared for session

### Implementation:
- Add encryption methods to DiarkisService or separate CryptoLayer
- Use System.Security.Cryptography.Aes for AES-256
- Add GCM mode support (may need to use Bouncy Castle if unavailable in base library)

## 2.4 Heartbeat & Keep-Alive

### Objectives:
- Send periodic heartbeat messages to Diarkis server
- Detect connection loss
- Implement automatic reconnection

### Implementation:
- Send heartbeat every 30 seconds
- Use Timer or PeriodicTimer (new in .NET 6+)
- Implement exponential backoff for reconnection attempts
- Max 5 reconnection attempts with delays: 1s, 2s, 4s, 8s, 16s

## 2.5 Testing Strategy

### Unit Tests:
- Connection establishment and cleanup
- Message serialization/deserialization
- Encryption/decryption round-trip
- Message routing dispatch

### Integration Tests:
- Full connect → authenticate → send message → disconnect flow
- Multiple concurrent rooms
- Player join/leave sequences

### Load Testing:
- 10-20 concurrent users
- 100+ messages per second
- Room with 4 players all moving simultaneously

## 2.6 Critical Decisions

**TCP vs UDP:**
- TCP: Reliable, ordered (used for room state, matchmaking)
- UDP: Low-latency, some loss acceptable (used for real-time transforms)

**Message Fragmentation:**
- Large messages > 1400 bytes may need fragmentation for UDP
- Implement sequence numbers for reassembly
- 5 second timeout for incomplete fragments

**Serialization:**
- Use existing MessagePack v3.1.4 from SekaiApiModel
- Create context for known types: Room, RoomSyncData, NetworkObject, DynamicPropertyPayload
- Cache serialized forms of frequently sent messages

**Session Management:**
- Session ID from auth endpoint
- Bind session to user ID
- Invalidate session on disconnect
- Support session resume within 60 seconds

## 2.7 Dependencies

Already Available:
- DiarkisOptions ✓
- UserDiarkisAuthResponse ✓
- MessagePack v3.1.4 ✓
- ASP.NET Core DI ✓
- IMemoryCache ✓

May Need to Add:
- Bouncy Castle (if GCM not in System.Security.Cryptography)
- ProtoBuf-net (if using alternative serialization)

## 2.8 Performance Targets

- Connection establishment: < 100ms
- Message round-trip: < 50ms (TCP), < 20ms (UDP)
- Memory per connection: < 5MB
- CPU per connection: < 1% (at 60 fps updates)

## 2.9 Rollout Plan

1. **Week 1:** DiarkisService core, basic TCP connection
2. **Week 1:** Message routing framework
3. **Week 2:** Encryption layer
4. **Week 2:** Heartbeat and reconnection
5. **Week 3:** Testing and optimization

## 2.10 Success Criteria

- [ ] Project compiles with zero warnings
- [ ] RoomService can be instantiated via DI
- [ ] DiarkisService can connect to Diarkis server
- [ ] Messages can be sent and received
- [ ] Encryption/decryption works correctly
- [ ] Heartbeat keeps connection alive for 5+ minutes
- [ ] Can handle disconnection and reconnect automatically
- [ ] All unit tests pass
- [ ] No memory leaks on long-running connections

## Next Immediate Task

Begin implementing DiarkisService.cs with:
1. Connection setup (TCP socket)
2. Session initialization with encryption keys
3. Basic send/receive loop
4. Error handling and logging

Estimated effort: 2-3 hours of focused development.
