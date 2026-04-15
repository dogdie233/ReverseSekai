# MultiLive 多人游戏协议逆向分析

> 基于 `libil2cpp.so` (ARM64) + Il2CppDumper `dump.cs` 逆向分析
> 游戏: Project SEKAI (Colorful Stage)
> 分析日期: 2026-04-15

## 目录

- [1. 总体技术架构](#1-总体技术架构)
- [2. 双通道通信模型](#2-双通道通信模型)
- [3. Diarkis 实时服务器协议](#3-diarkis-实时服务器协议)
- [4. 房间生命周期与状态机](#4-房间生命周期与状态机)
- [5. 匹配流程](#5-匹配流程)
- [6. 游戏中实时同步](#6-游戏中实时同步)
- [7. HTTP API 端点](#7-http-api-端点)
- [8. 数据结构参考](#8-数据结构参考)

---

## 1. 总体技术架构

### 1.1 双通道设计

游戏采用 **双通道架构** 进行多人协作:

```
┌──────────────┐    HTTPS (AES-CBC/MsgPack)     ┌───────────────────────────┐
│              ├────────────────────────────────►│  production-game-api      │
│  Game Client │    REST API (事务性操作)         │  .sekai.colorfulpalette   │
│  (Unity/     │                                 │  .org                     │
│   IL2CPP)    │                                 └───────────────────────────┘
│              │
│              │    Diarkis UDP/TCP               ┌───────────────────────────┐
│              ├────────────────────────────────►│  Diarkis Server           │
│              │    (加密, 端口 7100)              │  (GCP, *.googleusercontent│
└──────────────┘    (实时房间同步)                 │  .com)                    │
                                                  └───────────────────────────┘
```

- **HTTPS 通道**: AES-256-CBC 加密 + MessagePack 序列化, 用于事务性操作 (开始/结束Live, 认证, 奖励结算)
- **Diarkis 通道**: 基于 UDP (可选TCP回退) 的实时协议, 用于房间状态同步, 匹配, 实时分数/技能同步

### 1.2 关键命名空间

| 命名空间 | 职责 |
|---------|------|
| `Sekai` | 核心 API 请求/响应 DTO, 枚举, 主数据 |
| `Sekai.MultiLive` | 多人Live房间步骤/状态/消息类型, 匹配数据 |
| `Sekai.Multiplay` | 房间/玩家属性常量 (Photon 时代遗留命名), 派对成员数据 |
| `Sekai.CheerfulCarnival` | 应援嘉年华 (Cheerful Live) 专用数据 |
| `Sekai.StreamingLive` | Connect Live (虚拟Live) 专用数据 |
| `Sekai.RankLive.Realtime` | 段位赛 (Rank Match) 专用数据 |
| `CP.Realtime` | 底层实时框架 (Colorful Palette 自研, 包装 Diarkis SDK) |
| `Diarkis` / `Diarkis.Modules` | Diarkis SDK 原生模块 (Room, MatchMaker, Group, P2P) |

### 1.3 历史演变

游戏最初使用 **Photon (PUN)** 作为实时框架, 后迁移至 **Diarkis**。证据:
- `MultiLiveRequest` 中仍有 `photonRoomName` 字段
- `MasterMultiLiveLobby` 有 `photonLobbyName` 字段
- 大厅名称: `publicFreeMultiLive`, `publicVeteranMultiLive_{SCALE_INDEX}`
- 但实际协议层已完全是 Diarkis (`Diarkis.Core`, `Diarkis.Udp`, `Diarkis.Tcp`)

### 1.4 序列化

所有数据使用 **MessagePack** 序列化:
- HTTP API: `MsgPack 序列化` → `AES-256-CBC 加密` → `application/octet-stream`
- Diarkis 通道: `MsgPack 序列化` → `Diarkis.Lib.Encryption (AES + HMAC)` → `UDP/TCP`

---

## 2. 双通道通信模型

### 2.1 完整的 MultiLive 会话流程

```
时间线    客户端                          HTTP API 服务器              Diarkis 服务器
  │
  │   ══ Phase 1: 前置检查 ══
  ├──► GET /api/module-maintenance/MULTI_LIVE ──►
  │                                    ◄── { isOngoing: false }
  │
  │   ══ Phase 2: Diarkis 认证 ══
  ├──► GET /api/user/{id}/diarkis-auth?diarkisServerType=multi ──►
  │                                    ◄── { udpHost, udpPort, clientKey,
  │                                          sid, encryptionKey/Iv/MacKey }
  │
  │   ══ Phase 3: 连接 Diarkis & 匹配 ══
  ├───────────────────────────────────────────────► UDP Connect (port 7100)
  │                                                 SetEncryptionKeys(sid, key, iv, mac)
  │                                                 SetClientKey(clientKey)
  ├───────────────────────────────────────────────► MatchMaker.Search / Room.JoinRandom
  │                                                 ...匹配中...
  │                                          ◄──── Room.OnJoin (roomSyncData)
  │
  │   ══ Phase 4: 房间内等待 ══
  │   [玩家加入, 状态同步, 选歌, 投票, 倒计时...]
  ├───────────────────────────────────────────────► Room.SendProperty (状态变更)
  │                                          ◄──── Room.OnMemberBroadcast (其他玩家状态)
  │
  │   ══ Phase 5: 准备开始 ══
  ├──► POST /api/user/{id}/stamp-use-history ──►   (上报表情使用记录)
  ├──► GET  /api/user/{id}/ingame-cutin?userId2=...&...  ──►  (获取 cutin 动画)
  │
  │   ══ Phase 6: 开始 Live ══
  ├──► POST /api/user/{id}/multi-live/{liveId} ──►  (扣除体力/boost, 开始Live)
  │                                    ◄── { updatedResources, liveId }
  │
  │   ══ Phase 7: 游戏进行中 (实时同步) ══
  │   [分数/连击/Fever/技能 通过 Diarkis UDP 实时广播]
  ├───────────────────────────────────────────────► SendMessage(2000, PlayerLiveInfo)
  │                                          ◄──── OnMemberMessage (其他玩家 LiveInfo)
  │
  │   ══ Phase 8: 提交结果 ══
  ├──► PUT /api/user/{id}/multi-live/{liveId} ──►   (提交5人成绩)
  │                                    ◄── { scoreRank, rewards, eventPoints, ... }
  │
  │   ══ Phase 9: 断开/继续 ══
  ├───────────────────────────────────────────────► Room.Leave / 继续下一局
```

### 2.2 抓包验证

实际抓包数据 (2026-03-25, 来自 `traffic_logs/`):

**POST /api/user/{id}/multi-live/{liveId}** (开始):
```json
{
  "photonRoomName": "18a01a55ecc07f830a00001e1fa4000000000000000000000000",
  "privateFlg": false,
  "multiLiveLobbyId": 1,
  "musicId": 716,
  "deckId": 2,
  "musicDifficultyId": 3579,
  "musicVocalId": 1755,
  "boostCount": 10,
  "musicCategoryName": "image",
  "privateRoomSettings": null
}
```
> `photonRoomName` 是 Diarkis 房间的 hex 编码标识符 (历史命名)

**PUT /api/user/{id}/multi-live/{liveId}** (提交结果):
```json
{
  "score1": {
    "userId": 123456789,
    "musicDifficultyId": 3580,
    "score": 1660970,
    "perfectCount": 720,
    "greatCount": 24,
    "goodCount": 0,
    "badCount": 0,
    "missCount": 0,
    "maxCombo": 744,
    "life": 1000,
    "tapCount": 744
  },
  "score2": { "userId": ..., "score": 1402018, ... },
  "score3": { ... },
  "score4": { ... },
  "score5": { ... },
  "totalScore": 8253316,
  "superFeverFlg": false,
  "disconnectUserIds": [],
  "isMirrored": false,
  "ingameCutinCharacterArchiveVoiceGroupIds": [...]
}
```
> 注意: 不同玩家可以选择**不同难度** (`musicDifficultyId` 可以不同)

---

## 3. Diarkis 实时服务器协议

### 3.1 认证与连接

客户端通过 HTTP API 获取 Diarkis 连接凭证:

```
GET /api/user/{userId}/diarkis-auth?diarkisServerType=multi
```

**响应 `UserDiarkisAuthResponse`:**

| 字段 | 类型 | 说明 |
|-----|------|------|
| `userId` | long | 用户ID |
| `clientKey` | string | 会话客户端密钥 (UUID) |
| `udpHost` | string | Diarkis UDP 服务器地址 |
| `udpPort` | int | UDP 端口 (通常 7100) |
| `tcpHost` | string | TCP 回退地址 (可选) |
| `tcpPort` | int | TCP 端口 (可选) |
| `sid` | string | 会话ID (hex) |
| `encryptionKey` | string | AES 加密密钥 |
| `encryptionIv` | string | AES 初始向量 |
| `encryptionMacKey` | string | HMAC 签名密钥 |

**连接流程 (`MultiplayCore.Authentication`):**
1. 调用 `GetUserDiarkisAuth` API 获取凭证
2. 创建 UDP 客户端: `CreateUdp(sendInterval=200, echoInterval=5000, cmdVer=2)`
3. 设置加密: `SetEncryptionKeys(sid, encKey, encIV, encMacKey)`
4. 设置客户端标识: `SetClientKey(clientKey)`
5. 连接: `ConnectUDP(udpHost, udpPort)`

### 3.2 Diarkis 传输层

#### 数据包格式

```
┌──────────────────────────────────────────────┐
│  Packet.Header (固定)                         │
│  ┌────────┬────────┬────────┬──────────────┐ │
│  │ Ver(4) │ Cmd(4) │Status(4)│PayloadSize(4)│ │
│  └────────┴────────┴────────┴──────────────┘ │
│  Payload (MsgPack 序列化的数据, 经 AES+HMAC)   │
└──────────────────────────────────────────────┘
```

#### 加密层 (`Diarkis.Lib.Encryption`)

- 加密: `EncryptAndSign(key, iv, macKey, payload)` — AES 加密 + HMAC-SHA256 签名
- 解密: `AuthAndDecrypt(key, iv, macKey, payload)` — 先验证 HMAC, 再 AES 解密
- HMAC 长度: 32 字节
- AES 块大小: 16 字节

#### UDP 可靠传输 (RUDP)

Diarkis UDP 实现了 RUDP (Reliable UDP):
- `_rudpSeq` — 序列号计数器
- `_rudpOuts` — 发送队列 (待确认)
- `_rudpAcks` — ACK 队列
- `_rudpRetries` — 重试计数
- `_rudpRetryInterval` — 重试间隔
- `_rudpMaxRetry` — 最大重试次数
- 支持**分包**: `_splitPackets` / `_splitPacketOutID` (大于 `MAX_PACKET_SIZE=1300` 字节自动分包)

#### 连接常量

| 常量 | 值 | 说明 |
|-----|---|------|
| `MAX_PACKET_SIZE` | 1300 | UDP 最大包大小 |
| `MAX_PACKET_SIZE_IN` | 1400 | 接收最大包 |
| `RCV_POLL_TIME` | 10000 | 接收轮询时间(us) |
| `ECHO_SEND_GIVEUP_COUNT` | 5 | 心跳丢失断连阈值 |
| `DISCONN_NO_RESPONSE` | 1 | 无响应断连 |
| `DISCONN_RETRY_TIMEOUT` | 2 | 重试超时断连 |

### 3.3 Diarkis 模块命令 ID

#### Room 模块

| 命令 | ID | 说明 |
|-----|----|------|
| `CREATE_CMD` | 100 | 创建房间 |
| `JOIN_CMD` | 101 | 加入房间 |
| `LEAVE_CMD` | 102 | 离开房间 |
| `BROADCAST_CMD` | 103 | 广播消息 |
| `MESSAGE_CMD` | 104 | 发送消息 |
| `RAND_CREATE_CMD` | 105 | 随机创建 |
| `RAND_JOIN_CMD` | 106 | 随机加入 |
| `UPDATE_PROP_CMD` | 107 | 更新属性 |
| `GET_PROP_CMD` | 108 | 获取属性 |
| `GET_OWNER_CMD` | 109 | 获取房主 |
| `INCR_PROP_CMD` | 10 | 属性自增 |
| `GET_MEMBERS_CMD` | 11 | 获取成员列表 |
| `MIGRATE_CMD` | 12 | 房间迁移 |
| `GET_NUM_OF_MEMBERS_CMD` | 13 | 获取成员数 |
| `OWNER_CHANGE_CMD` | 14 | 房主变更 |
| `REG_ROOM_CMD` | 115 | 注册房间 |
| `FIND_ROOMS_CMD` | 116 | 查找房间 |
| `RESERVE_CMD` | 117 | 预约房间 |
| `CANCEL_RES_CMD` | 118 | 取消预约 |
| `CHAT_CMD` | 125 | 聊天 |
| `CHAT_LOG_CMD` | 126 | 聊天日志 |
| `P2P_INIT_CMD` | 127 | P2P 初始化 |
| `OBJ_SYNC_CMD` | 128 | 物体同步 |
| `OBJ_UPDATE_CMD` | 129 | 物体更新 |
| `PROP_SYNC_CMD` | 130 | 属性同步 |
| `RELAY_CMD` | 18 | 中继 |
| `RELAY_PROFILE_CMD` | 19 | 中继 Profile |

#### MatchMaker 模块

| 命令 | ID | 说明 |
|-----|----|------|
| `WAIT_CMD` | 200 | 等待匹配 |
| `SEARCH_CMD` | 201 | 搜索匹配 |
| `REMOVE_CMD` | 202 | 移除 |
| `LEAVE_CMD` | 203 | 离开匹配 |
| `SYNC_CMD` | 204 | 同步 |
| `CLAIM_CMD` | 205 | 认领 |
| `COMPLETE_CMD` | 206 | 完成 |
| `RESULTS_CMD` | 207 | 结果 |
| `P2P_CMD` | 208 | P2P |
| `NEW_TEAM_CMD` | 209 | 新建队伍 |
| `BACKFILL_CMD` | 211 | 回填 |
| `TEAM_SEARCH_CMD` | 214 | 队伍搜索 |
| `COMMIT_CMD` | 215 | 提交 |
| `COMP_COMMIT_CMD` | 216 | 完成提交 |
| `KICK_CMD` | 217 | 踢出 |
| `TICKET_CMD` | 218 | 票据 |
| `TICKET_COMP` | 220 | 票据完成 |
| `HOST_CHANGE` | 221 | 主机变更 |
| `TICKET_CANCEL_CMD` | 222 | 取消票据 |
| `TICKET_MATCH` | 223 | 票据匹配 |
| `TICKET_BROADCAST` | 224 | 票据广播 |

#### Group 模块

| 命令 | ID | 说明 |
|-----|----|------|
| `CREATE_CMD` | 110 | 创建组 |
| `JOIN_CMD` | 111 | 加入组 |
| `LEAVE_CMD` | 112 | 离开组 |
| `BROADCAST_CMD` | 113 | 组广播 |
| `RAND_JOIN_CMD` | 114 | 随机加入组 |

### 3.4 游戏层自定义命令 ID (CP.Realtime.Room)

在 Diarkis 基础协议之上, 游戏层定义了一系列自定义命令:

| 常量 | ID | 说明 |
|-----|----|------|
| `CUSTOM_RAND_JOIN_CMD` | 1010 | 随机加入 |
| `CUSTOM_RAND_ROOM_JOIN_CMD` | 10111 | 随机房间加入 |
| `PLAYER_INFO_CMD` | 1011 | 请求玩家信息 |
| `ROOM_SYNC_CMD` | 1012 | 完整房间同步 |
| `ROOM_SYNC_MINIMAL_CMD` | 1022 | 精简房间同步 |
| `CLOSE_MATCHMAKE_CMD` | 1013 | 关闭匹配 |
| `CLOSE_ROOM_CMD` | 1024 | 关闭房间 |
| `CHANGE_MATCHMAKE_CONDITION_CMD` | 1014 | 修改匹配条件 |
| `ADD_MATCHMAKE_CONDITION_CMD` | 1034 | 添加匹配条件 |
| `OPEN_ROOM_CMD` | 1023 | 开放房间 |
| `SCALEUP_MATCHMAKE_CONDITION_CMD` | 1015 | 放宽匹配条件 |
| `RESET_SCALEUP_MATCHMAKE_CMD` | 1025 | 重置放宽 |
| `MATCHING_MOVE_AND_SCALEUP_CMD` | 1035 | 移动+放宽 |
| `CUSTOM_CREATE_CMD` | 1016 | 自定义创建房间 |
| `CUSTOM_JOIN_CMD` | 1019 | 自定义加入 |
| `UNLOCK_JOIN_CMD` | 1049 | 解锁加入 (私人房) |
| `JOIN_POST_PROCESS` | 1032 | 加入后处理 |
| `JOIN_POST_PROCESS_MINIMAL` | 1042 | 精简加入后处理 |
| `UPDATE_ROOM_PROPERTY_CMD` | 10010 | 更新房间属性 |
| `UPDATE_PLAYER_PROPERTY_CMD` | 10020 | 更新玩家属性 |
| `UPDATE_PLAYER_PROPERTY_AND_INDEX_CMD` | 10030 | 更新玩家属性+索引 |
| `REFRESH_USER_INDEX_CMD` | 1021 | 刷新用户索引 |
| `RELEASE_PRIVATE_ROOM_CMD` | 2000 | 公开私人房间 |
| `MATCHING_ROOM_MOVE_CMD` | 1020 | 匹配房间移动 |
| `DIRECT_ROOM_MOVE_CMD` | 1029 | 直接移动 |
| `FORCE_DIRECT_ROOM_MOVE_CMD` | 1059 | 强制移动 |
| `CUSTOM_RECREATE_CMD` | 1300 | 重建房间 |
| `TIMESTAMP_CMD` | 10000 | 服务器时间戳 |
| `CHANGE_OWNER_CMD` | 998 | 房主变更 |
| `ROOM_MEMBER_CMD` | 600 | 房间成员列表 |

### 3.5 游戏层 MultiLive 专用命令 ID (`MultiplayConstCommon`)

这些命令运行在 CP.Realtime 之上, 版本号 `CMD_VERSION = 2`:

| 常量 | ID | 说明 |
|-----|----|------|
| `MESSAGE_COUNT_DOWN` | 1000 | 倒计时消息 |
| `MESSAGE_STAMP` | 1001 | 表情消息 |
| `MESSAGE_LOAD_PROGRESS` | 1002 | 加载进度 |
| `MESSAGE_RECEIVE_PLAYER_LIVE_INFO` | 2000 | 实时分数/连击同步 |
| `MESSAGE_RECEIVE_PLAYER_PRAISE_INFO` | 2001 | 点赞信息 |
| `MESSAGE_RECEIVE_PLAYER_SKILL_INFO` | 2002 | 技能激活通知 |
| `MULTI_LIVE_CUSTOM_RAND_JOIN_CMD` | 3000 | MultiLive 随机加入 |
| `MULTI_LIVE_CUSTOM_RAND_ROOM_JOIN_CMD` | 3001 | MultiLive 随机房间加入 |
| `MULTI_LIVE_CLOSE_MATCHMAKE_CMD` | 3030 | 关闭匹配 |
| `MULTI_LIVE_ADD_MATCHMAKE_CONDITION_CMD` | 3040 | 添加匹配条件 |
| `MULTI_LIVE_SCALEUP_MATCHMAKE_CONDITION_CMD` | 3050 | 放宽匹配 |
| `MULTI_LIVE_CUSTOM_CREATE_CMD` | 3060 | 创建自定义房间 |
| `MULTI_LIVE_CREATE_CMD` | 3070 | 创建标准房间 |
| `MULTI_LIVE_CUSTOM_JOIN_CMD` | 3080 | 自定义加入 |
| `RELEASE_MULTI_LIVE_PRIVATE_ROOM_CMD` | 3090 | 公开私人房间 |
| `MULTI_LIVE_PRIVATE_CLOSE_MATCHMAKE_CMD` | 3100 | 关闭私人匹配 |
| `MULTI_LIVE_RE_START_PRIVATE_CMD` | 3110 | 重启私人房间 |
| `MULTI_LIVE_UNLOCK_JOIN_CMD` | 3120 | 解锁加入私人房间 |
| `UPDATE_TOTAL_POWER_LIMIT_CMD` | 10040 | 更新战力限制 |

---

## 4. 房间生命周期与状态机

### 4.1 房间步骤 (`RoomStepType`)

```
Entrance(0) → Matching(1) → MusicSelect(2) → Shuffle(3) → ReadyFinal(4) → Loading(5) → Live(6) → Result(7)
    │                                                                                                    │
    └────────────────────────────────────────── 循环 ◄──────────────────────────────────────────────────┘
```

| 步骤 | 值 | 说明 |
|-----|----|------|
| `Entrance` | 0 | 入口/大厅 |
| `Matching` | 1 | 匹配中, 等待人数满 |
| `MusicSelect` | 2 | 选歌 (每人提交一首) |
| `Shuffle` | 3 | 随机抽选最终曲目 |
| `ReadyFinal` | 4 | 最终确认 (选难度) |
| `Loading` | 5 | 加载资源 |
| `Live` | 6 | 演奏中 |
| `Result` | 7 | 结果显示 |

### 4.2 玩家状态 (`PlayerStatus`)

```
None(0) → Matching(1) → FixedParty(2) → SelectMusic(3) → FixedMusic(4) → SelectDifficulty(5)
  → ReadyToLive(6) → Loading(7) → LoadComplete(8) → PrepareAPIFinished(9) → InGameLiveReady(10)
  → Live(11) → LiveFinished(12) → WaitResult(13) → Result(14)
```

### 4.3 触发消息类型 (`InvokeMessageType`)

房主通过设置房间属性 `"MESSAGE"` 来广播全局事件:

| 值 | 说明 |
|----|------|
| `None` (0) | 无 |
| `FixedParty` (1) | 队伍确定 (人满) |
| `DoShuffle` (2) | 执行歌曲抽选 |
| `StartLive` (3) | 开始 Live |
| `StartResult` (4) | 开始结果展示 |

### 4.4 房间属性标志位 (`MultiLiveRoomProperty` Flags)

使用位掩码管理房间状态:

| 标志 | 值 | 说明 |
|-----|----|------|
| `FLAG_FIXED_PARTY` | 1 | 队伍已确定 |
| `FLAG_FIXED_MUSIC_ALL` | 2 | 全员已选歌 |
| `FLAG_FIXED_DIFFICULTY_ALL` | 4 | 全员已选难度 |
| `FLAG_LOAD_COMPLETE_ALL` | 8 | 全员加载完成 |
| `FLAG_MASTER_START_API_FINISHED` | 16 | 房主 API 调用完成 |
| `FLAG_PREPARE_SETTING_COMPLETE` | 32 | 准备设置完成 |
| `FLAG_LIVE_READY_ALL` | 64 | 全员 Live 就绪 |
| `FLAG_LIVE_START` | 128 | Live 开始 |
| `FLAG_LIVE_RESULT` | 256 | Live 结果 |

### 4.5 房间属性键 (`RoomPropertyConstCommon`)

| 常量 | Key | 说明 |
|-----|-----|------|
| `INVOKE_MESSAGE` | `"MESSAGE"` | 触发消息 |
| `ROOM_STEP` | `"STEP"` | 当前步骤 |
| `TEAM_ID` | `"TEAM_ID"` | 队伍ID |
| `ROOM_ENTER_TYPE` | `"ATYPE"` | 入口类型 (Public/Private/Reserve) |
| `MASTER_MULTI_LOBBY_ID` | `"MASTER_LOBBY_ID"` | 大厅ID |
| `LIVE_ID` | `"LIVE_ID"` | Live 唯一标识 |
| `RANDOM_SEED` | `"RANDOM_SEED"` | 随机种子 |
| `MATCH_RECRUIT_TOTAL_POWER` | `"RECRUIT_TOTAL_POWER"` | 招募战力 |
| `MATCH_PRIVATE_ROOM_PUBLISH` | `"IS_PUBLISH"` | 是否公开 |
| `MATCH_PRIVATE_NUMBER` | `"ROOM_NUMBER"` | 私人房间号 |
| `MATCH_SCALEUP_FINISHED` | `"MATCH_SCALEUP_FINISH"` | 放宽完成 |
| `START_LIVE_TIMESTAMP` | `"START_LIVE_TIMESTAMP"` | Live 开始时间戳 |
| `LIVE_RULE_TYPE` | `"LIVE_RULE_TYPE"` | Live 规则类型 |
| `TOTAL_POWER_UPPER_LIMIT` | `"TOTAL_POWER_UPPER_LIMIT"` | 战力上限 |
| `TOTAL_POWER_LOWER_LIMIT` | `"TOTAL_POWER_LOWER_LIMIT"` | 战力下限 |
| `CUSTOM_ROOM_SETTING_DATA` | `"CUSTOM_ROOM_SETTING_DATA"` | 自定义房间设置 |
| `SHOWS_ROOM_ID` | `"SHOWS_ROOM_ID"` | 显示房间ID |
| `MASTER_TIMESTAMP` | `"MASTER_TIMESTAMP"` | 房主时间戳 |

MultiLive 额外属性:
| 常量 | Key |
|-----|-----|
| `SELECTED_MUSIC_ID` | `"SELECTED_MUSIC_ID"` |
| `SELECTED_MUSIC_USER_ID` | `"SELECTED_MUSIC_USER_ID"` |

### 4.6 玩家属性键 (`PlayerPropertyConstCommon`)

| 常量 | Key | 说明 |
|-----|-----|------|
| `BASIC_INFO` | `"BASIC_INFO"` | 基本信息 |
| `STATUS` | `"STATUS"` | 玩家状态 |
| `JOIN_ROUTE` | `"JOIN_ROUTE"` | 加入路径 |
| `SELECT_DIFFICULTY` | `"SELECT_DIFFICULTY"` | 选择难度 |
| `PARTY_INDEX` | `"PARTY_INDEX"` | 派对索引 |
| `MUSIC_ID` | `"MUSIC_ID"` | 选曲ID |
| `IS_ENTRUST` | `"IS_ENTRUST"` | 是否委托选曲 |
| `RESULT` | `"RESULT"` | 结果数据 |

旧版 (`MultiLivePlayerProperty`) 附加键:
| Key | 说明 |
|-----|------|
| `"CARD_ID"` | 卡片ID |
| `"CARD_LV"` | 卡片等级 |
| `"CARD_SKILL_LV"` | 技能等级 |
| `"CARD_MASTER_RANK"` | 特训等级 |
| `"CUNIT"` | 服装组合类型 |
| `"HAIR_COSTUME"` | 发型服装 |
| `"BODY_COSTUME"` | 身体服装 |
| `"ACCESSORY_COSTUME"` | 配饰 |
| `"TOTAL_POWER"` | 含Buff总战力 |
| `"IS_TRAINING"` | 是否特训 |
| `"IMAGE"` | 默认图片 |
| `"M_HONOR_ID"` / `"M_HONOR_LV"` | 主称号 |
| `"S_HONOR_IDS"` / `"S_HONOR_LVS"` | 副称号 |
| `"SELECT_MUSIC_ID"` | 选曲 |
| `"HAVE_MUSICS"` | 拥有歌曲 |
| `"JRT"` | 加入路径 |

### 4.7 属性同步机制 (`DynamicPropertyPayload`)

**核心数据结构** — 所有房间/玩家属性的载体:

```csharp
[MessagePackObject]
struct DynamicPropertyPayload {
    [Key("R")]      byte isRSend;                   // 0=不可靠, 1=可靠发送
    [Key("Values")] Dictionary<int, byte[]> values;  // 属性ID → MsgPack序列化值
}
```

**工作原理:**
1. `SyncProperty` 维护 `keyMap: Dict<string, Meta>`, 将属性名映射到 `(id: ushort, type: byte)`
2. 设置属性时, 值被序列化为 `byte[]` 存入 `prop` 字典, 同时标记 dirty (`chageProp`)
3. 发送时, 只发送 dirty 的属性, 打包成 `DynamicPropertyPayload`
4. `Meta.id` 作为 int 键, `byte[]` 作为值
5. 接收方用 `idToKeyMap` 反查属性名, 再反序列化回对应类型

**属性类型 (`SyncProperty.TYPE_*`):**

| 常量 | 值 | C# 类型 |
|-----|----|---------|
| `TYPE_BYTE` | 1 | byte |
| `TYPE_INT32` | 2 | int |
| `TYPE_INT64` | 3 | long |
| `TYPE_STRING` | 4 | string |
| `TYPE_FLOAT` | 5 | float |
| `TYPE_BOOL` | 6 | bool |
| `TYPE_OBJECT` | 7 | object (MsgPack) |

---

## 5. 匹配流程

### 5.1 大厅配置 (`MasterMultiLiveLobby`)

| ID | 名称 | 大厅名 | 匹配逻辑 | 最低战力 | 类型 |
|----|------|--------|---------|---------|------|
| 1 | フリールーム | `publicFreeMultiLive` | random | 0 | normal |
| 2 | ベテランルーム | `publicVeteranMultiLive_{SCALE_INDEX}` | power | 150000 | normal |
| 3 | フリーマッチ | `publicFreeCheerfulParty` | random | 0 | cheerful_carnival |
| 4 | ベテランマッチ | `publicVeteranCheerfulParty_{SCALE_INDEX}` | power | 150000 | cheerful_carnival |

### 5.2 匹配模式

- **random**: 纯随机匹配, 忽略战力
- **power**: 按战力匹配, 初始范围 ±10000, 每3秒放宽5000 (`multi_live_power_range_spread_second: 3000`)

### 5.3 匹配常量

| 常量 | 值 |
|-----|----|
| `LOBBY_NAME_RANDOM` | `"SEKAI_MULTI_LIVE_RANDOM"` |
| `LOBBY_NAME_TOTALPOWER` | `"SEKAI_MULTI_LIVE_TOTALPOWER"` |
| `LOBBY_NAME_PREFIX_PUBLIC` | `"PUBLIC_"` |
| `LOBBY_NAME_PREFIX_PRIVATE` | `"PRIVATE_"` |
| `LOBBY_NAME_PRIVATE_ROOM` | `"PRIVATE_SEKAI_MULTI_LIVE"` |
| `MAX_PLAYER` | 5 |
| `ROOM_COUNTDOWN_TIMER_SEC` | 30 |
| `KEEP_ALIVE_BACKGROUND_SEC` | 10 |
| `PLAYER_TTL_SEC` | 10 |
| `TOTALPOWER_MATCH_SEARCH_TIMEOUT` | 4 |
| `MUSIC_SELECT_COUNT_DOWN_SEC` | 20 |
| `FINAL_CONFIRAMATION_COUNT_DOWN_SEC` | 10 |

### 5.4 公开匹配流程

```
1. 客户端发送 SearchJoinOrCreate:
   {
     matchingName: "PUBLIC_SEKAI_MULTI_LIVE_RANDOM" 或 "PUBLIC_SEKAI_MULTI_LIVE_TOTALPOWER",
     mode: "free" 或 "veteran",
     totalPower: 250000,
     roomProperty: { ... },
     playerProperty: { ... }
   }

2. Diarkis 服务器搜索匹配的房间
   - random: 任意有空位的房间
   - power: 战力范围匹配

3. 如果找到 → Join + ROOM_SYNC
   如果没找到 → Create 新房间 + 等待其他玩家

4. 房间人数到 5 → 房主触发 FixedParty (InvokeMessageType=1)
   或 30秒倒计时结束 → 强制开始

5. MusicSelect 阶段 (20秒):
   - 每人选一首歌 → 设置玩家属性 "MUSIC_ID"
   - 可以选择 "委托" (IS_ENTRUST)

6. Shuffle:
   - 房主随机抽选 → 设置 SELECTED_MUSIC_ID / SELECTED_MUSIC_USER_ID

7. ReadyFinal (10秒):
   - 每人选难度 → SELECT_DIFFICULTY

8. Loading:
   - 房主调用 POST /api/user/{id}/multi-live/{liveId}
   - 全员加载 → 通过 MESSAGE_LOAD_PROGRESS (1002) 同步进度

9. Live → Result
```

### 5.5 私人房间流程

```
1. 创建者发送 CreateMultiLivePrivateRoomData:
   {
     multiLiveRuleType: "...",
     roomTTL: ...,
     roomProperty: { ... }
   }

2. 服务器返回 { roomId, roomCreateTime }

3. 其他玩家通过房间号加入:
   MultiLiveUnLockJoinData {
     roomId: "...",
     privateRoomNumber: 123456,
     totalPower: ...,
     playerProperty: { ... }
   }
   
   或直接加入:
   MultiLiveDirectJoinData {
     roomId: "...",
     totalPower: ...,
     playerProperty: { ... }
   }

4. 房主可以 "公开" 私人房间:
   ReleasePrivateRoomPayload {
     roomProperty: ...,
     totalPower: ...,
     mode: "..."
   }
```

---

## 6. 游戏中实时同步

### 6.1 消息类型

游戏中通过 `Room.SendMessage<T>(msgId, data, target, reliable)` 广播实时数据:

#### `PlayerLiveInfoPayload` (msgId: 2000)

实时分数/连击同步, MsgPack 按索引序列化:

| 索引 | 字段 | 类型 | 说明 |
|------|------|------|------|
| 0 | `combo` | int | 当前连击 |
| 1 | `totalCombo` | int | 总连击数 |
| 2 | `life` | int | 生命值 |
| 3 | `score` | int | 分数 |
| 4 | `baseTotalScore` | float | 基础总分 |
| 5 | `fever` | int | Fever 值 |
| 6 | `totalFever` | int | 总 Fever |
| 7 | `joinFever` | int | 加入 Fever |
| 8 | `technicalScore` | int | 技术分 |

#### `PlayerSkillInfoPayload` (msgId: 2002)

技能激活通知:

| 字段 | 类型 | 说明 |
|------|------|------|
| `userId` | string | 触发者 |
| `userIndex` | int | 玩家索引 |

#### `PlayerPraiseInfoPayload` (msgId: 2001)

点赞/应援:

| 字段 | 类型 | 说明 |
|------|------|------|
| `userId` | string | 点赞者 |
| `score` | int | 分数 |
| `count` | int | 次数 |
| `time` | float | 时间 |

#### 表情 (msgId: 1001)

通过 stamp ID 广播.

#### 加载进度 (msgId: 1002)

`LoadingProgressPayload` — float 进度值.

#### 倒计时 (msgId: 1000)

int 秒数.

### 6.2 网络对象同步

用于 Virtual Live 中的角色模型:

```csharp
struct NetworkObject {
    int viewId;         // 视图ID
    byte type;          // 0=用户, 1=房间
    uint objectType;    // 对象类型
    string prefab;      // Prefab 名称
    byte[] objectData;  // 自定义数据
    string ownerUserId; // 所有者
    Vec3D position;     // 位置
    Vec4D rotate;       // 旋转 (四元数)
    Vec3D scale;        // 缩放
}
```

Transform 同步:
```csharp
struct SyncTransformData {
    int viewId;
    uint flags;         // 1=Position, 2=Rotation, 4=Scale
    Vec3D position;
    Vec4D rotate;
    Vec3D scale;
    Vec3D speed;        // 移动速度
    float turnSpeed;    // 转向速度
    byte behavior;      // 0=平滑, 1=瞬移
}
```

---

## 7. HTTP API 端点

### 7.1 MultiLive 相关

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/module-maintenance/MULTI_LIVE` | 维护检查 |
| GET | `/api/user/{id}/diarkis-auth?diarkisServerType=multi` | Diarkis 认证 |
| POST | `/api/user/{id}/multi-live/{liveId}` | 开始 MultiLive (扣体力) |
| PUT | `/api/user/{id}/multi-live/{liveId}` | 提交结果 (5人成绩) |
| POST | `/api/user/{id}/stamp-use-history` | 上报表情使用 |
| GET | `/api/user/{id}/ingame-cutin?userId2=&userId3=&userId4=&userId5=` | 获取 cutin |
| POST | `/api/user/{id}/multi-live-penalty` | 上报断线惩罚 |

### 7.2 Diarkis 房间管理 (REST 辅助)

| 方法 | 路径 | 说明 |
|------|------|------|
| GET/POST | `/api/user/{id}/diarkis-room-type/{type}/no/{number}` | 按类型和号码查找房间 |
| GET/POST | `/api/user/{id}/diarkis-room/{roomId}` | 按 ID 查找房间 |
| GET/POST | `/api/user/{id}/diarkis-room/{roomId}/no` | 获取房间号码 |

### 7.3 应援嘉年华 (Cheerful Live)

| 方法 | 路径 | 说明 |
|------|------|------|
| POST | `/api/user/{id}/cheerful-carnival-live/{liveId}` | 开始 Cheerful Live |
| PUT | `/api/user/{id}/cheerful-carnival-live/{liveId}` | 提交 Cheerful Live 结果 |

### 7.4 段位赛 (Rank Match)

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/user/{id}/diarkis-auth?diarkisServerType=rank` | 段位赛 Diarkis 认证 |
| POST | `/api/user/{id}/rank-match-live/master/start` | 房主开始 |
| POST | `/api/user/{id}/rank-match-live/slave/start` | 客户端开始 |
| PUT | `/api/user/{id}/rank-match-live/finish` | 提交结果 |
| GET | `/api/user/{id}/rank-match-live/result` | 获取最终结果 |

---

## 8. 数据结构参考

### 8.1 核心房间同步数据

#### `RoomSyncData`
```
{
  "IsJoin":         bool,           // 是否加入
  "RoomCreateTime": uint,           // 创建时间
  "RoomID":         string,         // 房间ID
  "OwnerID":        string,         // 房主ID
  "RoomProperty":   DynamicPropertyPayload,
  "Players":        RoomSyncPlayer[],
  "NetworkObjects": NetworkObject[]
}
```

#### `MultiLiveRoomSyncData`
```
{
  "UserID":            string,
  "RoomCreateTime":    uint,
  "Index":             int,          // 自己的索引
  "PlayerProperty":    DynamicPropertyPayload,
  "RoomID":            string,
  "OwnerID":           string,
  "RoomProperty":      DynamicPropertyPayload,
  "Players":           RoomSyncPlayer[],
  "PrivateRoomNumber": long
}
```

#### `RoomSyncPlayer`
```
{
  "UserID":         string,
  "Index":          int,
  "PlayerProperty": DynamicPropertyPayload
}
```

### 8.2 匹配数据

#### `MultiLiveSearchJoinOrCreateData`
```
{
  "Mode":           string,    // "free" / "veteran"
  "TotalPower":     int,
  "RoomProperty":   DynamicPropertyPayload,
  "PlayerProperty": DynamicPropertyPayload
}
```

#### `CreateMultiLivePrivateRoomData`
```
{
  "MultiLiveRuleType": string,
  "RoomTTL":           int,
  "RoomProperty":      DynamicPropertyPayload
}
```

#### `MultiLiveDirectJoinData`
```
{
  "RoomID":         string,
  "TotalPower":     int,
  "PlayerProperty": DynamicPropertyPayload
}
```

#### `MultiLiveUnLockJoinData`
```
{
  "RoomID":                 string,
  "PrivateRoomNumber":      int,
  "TotalPowerUpperLimit":   int?,
  "TotalPowerLowerLimit":   int?,
  "TotalPower":             int,
  "PlayerProperty":         DynamicPropertyPayload
}
```

### 8.3 API 请求/响应

#### `UserMultiLiveRequest` (POST body)
```
{
  "photonRoomName":       string,   // Diarkis 房间 hex ID
  "privateFlg":           bool,
  "multiLiveLobbyId":     int,
  "musicId":              int,
  "deckId":               int,
  "musicDifficultyId":    int,
  "musicVocalId":         int,
  "boostCount":           int,
  "musicCategoryName":    string,
  "privateRoomSettings":  UserPrivateRoomSettings?
}
```

#### `MultiLiveRequest` (扩展版 POST body)
```
{
  // ...包含上述所有字段, 加上:
  "userId":               long,
  "userId1"~"userId5":    long,      // 5人 userId
  "selectedMusicId1"~"selectedMusicId5": int  // 5人选曲
}
```

#### `UserMultiLiveClearRequest` (PUT body)
```
{
  "score1"~"score5":      UserMultiLiveClearScoreRequest,
  "totalScore":           int,
  "superFeverFlg":        bool,
  "disconnectUserIds":    long[],
  "musicCategoryName":    string,
  "isMirrored":           bool,
  "ingameCutinCharacterArchiveVoiceGroupIds": int[],
  "privateRoomSettings":  UserPrivateRoomSettings?
}
```

#### `UserMultiLiveClearScoreRequest` (每人成绩)
```
{
  "userId":            long,
  "musicDifficultyId": int,
  "score":             int,
  "perfectCount":      int,
  "greatCount":        int,
  "goodCount":         int,
  "badCount":          int,
  "missCount":         int,
  "maxCombo":          int,
  "life":              int,
  "tapCount":          int
}
```

#### `UserPrivateRoomSettings`
```
{
  "liveRuleType":               string,
  "scoreCalculateType":         string,
  "musicSelectionType":         string,
  "musicDifficultyTypes":       string[],
  "deckTotalPowerRangeUpper":   int?,
  "deckTotalPowerRangeLower":   int?
}
```

#### `CustomRoomSettingData`
```
{
  "ScoreCalculateType":     ScoreCalculateType,
  "MusicSelectionType":     MusicSelectionType,
  "MusicDifficultyTypes":   MusicDifficulty[],
  "IsDisplayPlayerInfo":    bool
}
```

### 8.4 派对成员信息

#### `MultiLivePartyMember`
```
{
  "Index":                    int,
  "ActorNum":                 int,
  "UserId":                   string,
  "UserName":                 string,
  "CardId":                   int,
  "CardLv":                   int,
  "CardSkillLv":              int,
  "CardMasterRank":           int,
  "CostumeUnitType":          UnitType,
  "HairLiveCostumeId":        int,
  "BodyLiveCostumeId":        int,
  "AccessoryLiveCostumeId":   int,
  "TotalPowerIncludeBuff":    int,
  "IsTraining":               bool,
  "DefaultImage":             string,
  "SubCardIds":               int[],
  "SubCardSkillLv":           int[],
  "SubCardImages":            string[],
  "MainHonor":                RoomUserHonorInfo,
  "SubHonors":                RoomUserHonorInfo[],
  "Difficulty":               string,
  "ConnectStatus":            PartyMemberConnectStatus,
  "FriendRequestStatus":      FriendRequestStatus,
  "MemberCharacterRank":      MemberCharacterRank[],
  "PlayerFrameId":            int
}
```

### 8.5 结果响应

#### `UserMultiLiveClearResponse` (关键字段)
```
{
  "updatedResources":     SuiteUser,
  "scoreRank":            string,     // "S", "A", etc.
  "multiScoreRank":       string,
  "totalScore":           int,
  "user1"~"user5":        UserMultiLiveClearScoreResponse,
  "userExpResult":        UpdateExpResult,
  "deckCardExpResults":   DeckCardUpdateExpResult[],
  "unitExpResults":       UnitUpdateExpResult[],
  "scoreRankRewards":     UserResource[],
  "superFeverRewards":    UserResource[],
  "playerRankRewards":    UserResource[],
  "beforeEventPoint":     int,
  "afterEventPoint":      int,
  "isNuisance":           bool,       // 迷惑行为标记
  "activeUserIds":        long[],
  "isInBreakTime":        bool
}
```

### 8.6 枚举参考

#### `PartyMemberConnectStatus`
| 值 | 说明 |
|----|------|
| 0 | Disconnected (断线) |
| 1 | PreDisconnected (即将断线) |
| 2 | Connected (已连接) |

#### `RoomEnterType`
| 值 | 说明 |
|----|------|
| 0 | Public (公开) |
| 1 | Private (私人) |
| 2 | Reserve (预约) |

#### `MultiLiveType`
| 值 | 说明 |
|----|------|
| 0 | MultiLive (普通多人) |
| 1 | CheerfulLive (应援嘉年华) |

#### `LoginStatus`
| 值 | 说明 |
|----|------|
| 0 | offline |
| 1 | online |
| 2 | solo_live |
| 3 | multi_live |
| 4 | challenge_live |
| 5 | cheerful_live |
| 6 | virtual_live |
| 7 | rank_match |
| 8 | own_mysekai |
| 9 | other_mysekai |

---

## 附录: 配置常量

来自 `configs.json`:

| 配置项 | 值 | 说明 |
|-------|---|------|
| `multi_live_power_range_upper_limit_init` | 10000 | 战力匹配初始上限 |
| `multi_live_power_range_lower_limit_init` | 10000 | 战力匹配初始下限 |
| `multi_live_power_range_upper_limit_spread` | 5000 | 每次放宽上限 |
| `multi_live_power_range_lower_limit_spread` | 5000 | 每次放宽下限 |
| `multi_live_power_range_spread_second` | 3000 | 放宽间隔(ms) |
| `multi_live_cutin_max_count` | 5 | Cutin 最大数量 |
| `multi_live_super_star` | 1.3 | SuperStar 倍率 |
| `multi_live_connection_timeout` | - | 连接超时 |
| `multi_live_fever_progress` | - | Fever 进度率 |
| `multi_live_super_fever_progress` | - | Super Fever 进度率 |

`multiPlayVersion`: `"kaito"` — 客户端用于验证与 Diarkis 服务器版本兼容性.
