# PastPort API Reference

> **Base URL:** `https://your-domain.com/api`
> **Authentication:** JWT Bearer token (unless marked `Anonymous`)
> **Content-Type:** `application/json` (unless specified otherwise)

---

## Table of Contents

- [Error Response Format](#error-response-format)
- [Authentication](#1-authentication)
- [User Management](#2-user-management)
- [Historical Scenes](#3-historical-scenes)
- [Characters](#4-characters)
- [Conversations](#5-conversations)
- [NPC AI Sessions](#6-npc-ai-sessions)
- [SignalR NPC Hub](#7-signalr-npc-hub)
- [Assets (Admin)](#8-assets-admin)
- [Unity Assets](#9-unity-assets)
- [Subscriptions](#10-subscriptions)
- [Payments](#11-payments)
- [System](#12-system)

---

## Error Response Format

All error responses follow a consistent structure:

```json
{
  "error": "Human-readable error message",
  "statusCode": 400,
  "timestamp": "2026-04-26T02:00:00.0000000Z"
}
```

| HTTP Code | Meaning                                     |
| --------- | ------------------------------------------- |
| `400`     | Bad request / validation error              |
| `401`     | Unauthorized (missing or invalid JWT)       |
| `402`     | Payment required (subscription feature gate)|
| `404`     | Resource not found                          |
| `409`     | Conflict (e.g., duplicate subscription)     |
| `500`     | Internal server error                       |

---

## 1. Authentication

### POST `/api/auth/register`

Register a new user account.

**Auth:** Anonymous

**Request Body:**
```json
{
  "firstName": "Ahmed",
  "lastName": "Hassan",
  "email": "ahmed@example.com",
  "password": "SecureP@ss123!"
}
```

**Success Response:** `200 OK`
```json
{
  "success": true,
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "c4f2e8a1-...",
  "expiresAt": "2026-04-26T03:00:00Z"
}
```

**Error Response:** `400 Bad Request`
```json
{
  "success": false,
  "message": "Email already registered"
}
```

---

### POST `/api/auth/login`

Authenticate with email and password.

**Auth:** Anonymous

**Request Body:**
```json
{
  "email": "ahmed@example.com",
  "password": "SecureP@ss123!"
}
```

**Success Response:** `200 OK`
```json
{
  "success": true,
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "c4f2e8a1-...",
  "expiresAt": "2026-04-26T03:00:00Z"
}
```

---

### POST `/api/auth/refresh-token`

Exchange a valid refresh token for a new JWT + refresh token pair.

**Auth:** Anonymous

**Request Body:**
```json
{
  "refreshToken": "c4f2e8a1-..."
}
```

**Success Response:** `200 OK` — Same shape as login response.

---

### POST `/api/auth/logout`

Invalidate the current user's refresh token.

**Auth:** Bearer

**Success Response:** `200 OK`
```json
{
  "message": "Logged out successfully"
}
```

---

### POST `/api/auth/send-verification-code`

Send a verification code to the authenticated user's email.

**Auth:** Bearer

**Success Response:** `200 OK`
```json
{
  "success": true,
  "message": "Verification code sent"
}
```

---

### POST `/api/auth/verify-email`

Verify a user's email using the code sent via email.

**Auth:** Anonymous

**Request Body:**
```json
{
  "email": "ahmed@example.com",
  "code": "123456"
}
```

**Success Response:** `200 OK`
```json
{
  "success": true,
  "message": "Email verified successfully"
}
```

---

### POST `/api/auth/resend-verification-code`

Resend the email verification code.

**Auth:** Anonymous

**Request Body:**
```json
{
  "email": "ahmed@example.com"
}
```

---

### POST `/api/auth/forgot-password`

Initiate password reset flow. Always returns `200` to prevent email enumeration.

**Auth:** Anonymous

**Request Body:**
```json
{
  "email": "ahmed@example.com"
}
```

**Response:** `200 OK` *(always, regardless of whether the email exists)*

---

### POST `/api/auth/verify-reset-code`

Verify the password reset code.

**Auth:** Anonymous

**Request Body:**
```json
{
  "email": "ahmed@example.com",
  "code": "654321"
}
```

---

### POST `/api/auth/reset-password`

Reset the user's password using a verified reset code.

**Auth:** Anonymous

**Request Body:**
```json
{
  "email": "ahmed@example.com",
  "code": "654321",
  "newPassword": "NewSecureP@ss456!"
}
```

---

### GET `/api/auth/external-login/google`

Initiate Google OAuth2 login flow (server-side redirect).

**Auth:** Anonymous
**Query Params:** `returnUrl` (optional, default: `/`)
**Response:** `302 Redirect` to Google consent screen.

---

### GET `/api/auth/external-login/facebook`

Initiate Facebook OAuth login flow (server-side redirect).

**Auth:** Anonymous
**Query Params:** `returnUrl` (optional, default: `/`)

---

### GET `/api/auth/external-login/apple`

Initiate Apple Sign-In flow (server-side redirect).

**Auth:** Anonymous
**Query Params:** `returnUrl` (optional, default: `/`)

---

### GET `/api/auth/external-login-callback`

OAuth callback endpoint. Called automatically after provider consent.

**Query Params:** `provider` (required), `returnUrl` (optional)

**Success Response:** `200 OK` — JWT token + refresh token.

---

## 2. User Management

### GET `/api/users/profile`

Get the authenticated user's profile.

**Auth:** Bearer

**Success Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "id": "user-guid",
    "firstName": "Ahmed",
    "lastName": "Hassan",
    "email": "ahmed@example.com",
    "isEmailVerified": true,
    "createdAt": "2026-01-15T10:30:00Z",
    "lastLoginAt": "2026-04-26T01:00:00Z"
  }
}
```

---

### PUT `/api/users/profile`

Update the authenticated user's profile.

**Auth:** Bearer

**Request Body:**
```json
{
  "firstName": "Ahmed",
  "lastName": "Mohamed"
}
```

---

### DELETE `/api/users/account`

Permanently delete the authenticated user's account.

**Auth:** Bearer

**Success Response:** `200 OK`
```json
{
  "success": true,
  "message": "Account deleted successfully"
}
```

---

### GET `/api/users/stats`

Get user statistics (conversation count, join date).

**Auth:** Bearer

**Success Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "totalConversations": 42,
    "joinedDate": "2026-01-15T10:30:00Z"
  }
}
```

---

## 3. Historical Scenes

### GET `/api/scenes`

List all historical scenes.

**Auth:** Bearer

**Success Response:** `200 OK`
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "title": "Ancient Alexandria",
    "era": "300 BC",
    "location": "Alexandria, Egypt",
    "description": "The Great Library of Alexandria during its golden age",
    "environmentPrompt": "A grand marble library with papyrus scrolls...",
    "model3DUrl": "/assets/scenes/alexandria.glb",
    "createdAt": "2026-01-15T10:30:00Z"
  }
]
```

---

### GET `/api/scenes/{id}`

Get a specific scene by ID.

**Auth:** Bearer

---

### POST `/api/scenes`

Create a new historical scene.

**Auth:** Bearer

**Request Body:**
```json
{
  "title": "Roman Forum",
  "era": "100 AD",
  "location": "Rome, Italy",
  "description": "The political heart of the Roman Empire",
  "environmentPrompt": "A bustling Roman marketplace with marble columns...",
  "model3DUrl": "/assets/scenes/roman-forum.glb"
}
```

**Success Response:** `201 Created`

---

### PUT `/api/scenes/{id}`

Update an existing scene.

**Auth:** Bearer

---

### DELETE `/api/scenes/{id}`

Delete a scene.

**Auth:** Bearer

**Success Response:** `204 No Content`

---

## 4. Characters

### GET `/api/characters`

List all NPC characters.

**Auth:** Bearer

**Success Response:** `200 OK`
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "name": "Cleopatra VII",
    "role": "Pharaoh of Egypt",
    "background": "Last active ruler of the Ptolemaic Kingdom...",
    "personality": "Intelligent, diplomatic, charismatic",
    "voiceId": "voice_cleopatra_01",
    "avatarUrl": "/assets/avatars/cleopatra.png",
    "sceneId": "scene-guid"
  }
]
```

---

### POST `/api/characters`

Create a new NPC character.

**Auth:** Bearer

**Request Body:**
```json
{
  "name": "Julius Caesar",
  "role": "Dictator of Rome",
  "background": "Military general and statesman...",
  "personality": "Ambitious, strategic, eloquent",
  "voiceId": "voice_caesar_01",
  "avatarUrl": "/assets/avatars/caesar.png",
  "sceneId": "scene-guid"
}
```

---

### PUT `/api/characters/{id}`

Update a character.

**Auth:** Bearer

---

### DELETE `/api/characters/{id}`

Delete a character.

**Auth:** Bearer

---

## 5. Conversations

### GET `/api/conversations`

List conversation history for the authenticated user.

**Auth:** Bearer

**Success Response:** `200 OK`
```json
[
  {
    "id": "conv-guid",
    "characterId": "char-guid",
    "userMessage": "Tell me about the library",
    "characterResponse": "The Great Library of Alexandria was founded...",
    "createdAt": "2026-04-25T14:30:00Z"
  }
]
```

---

## 6. NPC AI Sessions

### POST `/api/npc/session/start`

Create a new NPC conversation session. The returned `sessionId` is passed to the SignalR hub.

**Auth:** Bearer
**Rate Limit:** 10 requests/minute per IP

**Request Body:**
```json
{
  "yearRange": "300 BC - 30 BC",
  "locationOldName": "Alexandria",
  "civilization": "Ptolemaic Egypt"
}
```

**Success Response:** `201 Created`
```json
{
  "sessionId": "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4",
  "expiresAt": "2026-04-26T04:00:00Z"
}
```

---

### GET `/api/npc/session/{sessionId}`

Check if a session is still valid.

**Auth:** Bearer

**Success Response:** `200 OK`
```json
{
  "sessionId": "a1b2c3d4e5f6a1b2c3d4e5f6a1b2c3d4",
  "civilization": "Ptolemaic Egypt",
  "yearRange": "300 BC - 30 BC",
  "locationOldName": "Alexandria",
  "createdAt": "2026-04-26T02:00:00Z"
}
```

**Not Found:** `404`
```json
{
  "message": "Session 'a1b2c3d4...' not found or expired."
}
```

---

## 7. SignalR NPC Hub

**Endpoint:** `/npcHub`
**Protocol:** SignalR (WebSocket transport preferred)
**Authentication:** JWT Bearer token passed via query string or headers

### Client → Server Methods

#### `StartConversation(sessionId, roleOrName, audioStream)`

Begin a streaming NPC voice conversation.

| Parameter     | Type                       | Description                                     |
| ------------- | -------------------------- | ----------------------------------------------- |
| `sessionId`   | `string`                   | Session ID from `/api/npc/session/start`        |
| `roleOrName`  | `string`                   | NPC character name or role to converse with     |
| `audioStream` | `IAsyncEnumerable<byte[]>` | Streamed audio chunks from the client microphone |

#### `EndSession(sessionId)`

Terminate a session early.

| Parameter   | Type     | Description             |
| ----------- | -------- | ----------------------- |
| `sessionId` | `string` | The active session ID   |

### Server → Client Events

| Event                 | Payload                              | Description                              |
| --------------------- | ------------------------------------ | ---------------------------------------- |
| `OnMetaReceived`      | `{ text, emotion, currentYear }`     | AI response metadata (text + emotion)    |
| `OnAudioReceived`     | `byte[]`                             | Raw AI-generated audio data              |
| `OnConversationDone`  | *(none)*                             | AI finished responding                   |
| `OnSessionError`      | `string`                             | Error message (timeout, invalid session) |

### Connection Example (C# / Unity)

```csharp
var connection = new HubConnectionBuilder()
    .WithUrl("https://api.pastport.com/npcHub", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult(jwtToken);
    })
    .WithAutomaticReconnect()
    .Build();

connection.On<object>("OnMetaReceived", meta =>
{
    Debug.Log($"NPC said: {meta}");
});

connection.On<byte[]>("OnAudioReceived", audioData =>
{
    // Play audio data in Unity
});

connection.On("OnConversationDone", () =>
{
    Debug.Log("Conversation complete");
});

await connection.StartAsync();
await connection.InvokeAsync("StartConversation", sessionId, "Cleopatra", audioStream);
```

---

## 8. Assets (Admin)

### GET `/api/assets`

List all uploaded assets.

**Auth:** Bearer

---

### GET `/api/assets/{id}`

Get asset metadata by ID.

**Auth:** Bearer

---

### POST `/api/assets/upload`

Upload a new 3D asset. Computes SHA-256 hash on upload for integrity checking.

**Auth:** Bearer
**Content-Type:** `multipart/form-data`

**Form Fields:**
| Field       | Type     | Required | Description                     |
| ----------- | -------- | -------- | ------------------------------- |
| `file`      | `file`   | Yes      | The asset file (.glb, .fbx, etc.) |
| `name`      | `string` | Yes      | Display name                    |
| `type`      | `string` | Yes      | Asset type (Model, Texture, Audio) |
| `sceneId`   | `guid`   | No       | Associate with a scene          |

**Success Response:** `201 Created`
```json
{
  "success": true,
  "data": {
    "id": "asset-guid",
    "name": "Egyptian Pillar",
    "fileName": "pillar_01.glb",
    "type": "Model",
    "fileUrl": "/uploads/assets/pillar_01_abc123.glb",
    "fileSize": 2457600,
    "fileHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "version": "1.0.0"
  }
}
```

---

### DELETE `/api/assets/{id}`

Delete an asset and its associated file.

**Auth:** Bearer

---

## 9. Unity Assets

Endpoints optimized for Unity client consumption. Some are `[AllowAnonymous]` to support Unity's asset discovery flow before user authentication.

### GET `/api/unityassets/search?name={name}`

Search for an asset by exact name match.

**Auth:** Anonymous
**Query Params:** `name` (required)

**Success Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "id": "asset-guid",
    "name": "chair_01",
    "fileName": "chair_01_abc.glb",
    "type": "Model",
    "fileUrl": "/uploads/assets/chair_01_abc.glb",
    "fileSize": 1024000,
    "fileHash": "sha256-hash",
    "version": "1.0.0"
  }
}
```

---

### GET `/api/unityassets/scene/{sceneId}`

Get all assets belonging to a specific scene.

**Auth:** Anonymous

**Success Response:** `200 OK`
```json
{
  "success": true,
  "data": [
    {
      "id": "asset-guid-1",
      "name": "pillar_01",
      "type": "Model",
      "fileHash": "sha256-hash",
      "version": "1.0.0"
    }
  ]
}
```

---

### GET `/api/unityassets/download/{assetId}`

Download the asset binary file.

**Auth:** Bearer

**Response:** Binary file with appropriate `Content-Type` header.

| Extension   | Content-Type              |
| ----------- | ------------------------- |
| `.glb`      | `model/gltf-binary`      |
| `.gltf`     | `model/gltf+json`        |
| `.fbx`/`.obj` | `application/octet-stream` |
| `.png`      | `image/png`              |
| `.jpg`      | `image/jpeg`             |
| `.mp3`      | `audio/mpeg`             |
| `.wav`      | `audio/wav`              |

---

### POST `/api/unityassets/verify`

Verify asset integrity by comparing hashes. Unity uses this to determine whether a local asset needs re-downloading.

**Auth:** Anonymous

**Request Body:**
```json
{
  "assetId": "asset-guid",
  "fileHash": "client-side-sha256-hash"
}
```

**Success Response:** `200 OK`
```json
{
  "success": true,
  "data": {
    "exists": true,
    "hashMatches": true,
    "needsDownload": false,
    "asset": {
      "id": "asset-guid",
      "name": "pillar_01",
      "fileUrl": "/uploads/assets/pillar_01.glb",
      "fileHash": "server-sha256-hash",
      "version": "1.0.0"
    }
  }
}
```

---

## 10. Subscriptions

### GET `/api/subscriptions/plans`

List all active, public subscription plans.

**Auth:** Anonymous

**Success Response:** `200 OK`
```json
[
  {
    "id": "plan-guid",
    "name": "Explorer Pro",
    "description": "Full access to all historical scenes",
    "price": 9.99,
    "currency": "USD",
    "billingCycle": "Monthly",
    "trialDays": 7,
    "features": [
      { "slug": "UnlimitedScenarios", "name": "Unlimited Scenarios" },
      { "slug": "ExploreSecrets", "name": "Hidden Artifacts Access" }
    ]
  }
]
```

---

### GET `/api/subscriptions/plans/{id}`

Get a single plan's details.

**Auth:** Anonymous

---

### GET `/api/subscriptions/me`

Get the authenticated user's current active subscription.

**Auth:** Bearer

**Success Response:** `200 OK` or `204 No Content` (if no active subscription)

---

### POST `/api/subscriptions/checkout`

Initiate a payment checkout session. Returns a URL to redirect the user to the payment gateway.

**Auth:** Bearer

**Request Body:**
```json
{
  "planId": "plan-guid",
  "successUrl": "https://app.pastport.com/payment/success",
  "cancelUrl": "https://app.pastport.com/payment/cancel"
}
```

**Success Response:** `200 OK`
```json
{
  "paymentUrl": "https://checkout.stripe.com/c/pay/...",
  "subscriptionId": "sub-guid",
  "transactionId": "tx-guid"
}
```

**Conflict:** `409`
```json
{
  "title": "Subscription conflict",
  "detail": "User already has an active subscription",
  "status": 409
}
```

---

### POST `/api/subscriptions/change-plan`

Upgrade or downgrade the user's current plan. Applies proration.

**Auth:** Bearer

**Request Body:**
```json
{
  "newPlanId": "plan-guid",
  "prorate": true
}
```

---

### POST `/api/subscriptions/cancel`

Cancel auto-renewal. User retains access until the current billing period ends.

**Auth:** Bearer

**Success Response:** `204 No Content`

---

### GET `/api/subscriptions/features/{slug}`

Check if the authenticated user's plan includes a specific feature.

**Auth:** Bearer

**Success Response:** `200 OK`
```json
{
  "featureSlug": "ExploreSecrets",
  "hasAccess": true
}
```

---

## 11. Payments

### GET `/api/payments/transactions`

Get the authenticated user's transaction history.

**Auth:** Bearer

---

### GET `/api/payments/invoices`

Get all invoices for the authenticated user.

**Auth:** Bearer

---

### POST `/api/payments/webhooks/stripe`

Stripe webhook receiver. Validates signature using `Stripe-Signature` header.

**Auth:** Anonymous
**Content-Type:** `application/json`

**Expected Stripe Events:**
- `checkout.session.completed` — Activates subscription
- `payment_intent.payment_failed` — Marks subscription as PastDue
- `charge.refunded` — Processes refund

**Response:** `200 OK` (always return quickly to avoid retries)

---

### POST `/api/payments/webhooks/paymob`

Paymob webhook receiver. Validates HMAC-SHA512 from `hmac` query parameter.

**Auth:** Anonymous
**Query Params:** `hmac` (required)
**Content-Type:** `application/json`

**Response:** `200 OK`

---

## 12. System

### GET `/api/test`

Smoke test endpoint.

**Auth:** Anonymous

**Response:** `200 OK`
```json
{
  "message": "PastPort API is working!",
  "timestamp": "2026-04-26T02:00:00Z",
  "version": "1.0.0"
}
```

---

### GET `/api/test/health`

Health check endpoint.

**Auth:** Anonymous

**Response:** `200 OK`
```json
{
  "status": "Healthy",
  "database": "Connected",
  "timestamp": "2026-04-26T02:00:00Z"
}
```
