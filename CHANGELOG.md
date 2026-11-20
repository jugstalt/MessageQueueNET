# Changelog

All notable changes to this project are documented in this file.

## [4.0.0] - unreleased

### Security

- **Path traversal**: queue names are validated against a strict pattern before they are used.
  - Allowed: lowercase letters, digits, `_`, `-`, and `.` (a period must be followed by another character).
  - Names must start with a letter or digit and are limited to 128 characters.
  - The filesystem persistence additionally verifies that every path stays inside `Persist:RootPath`.
- **Authentication runs first**: the basic authentication middleware now runs before Swagger, routing, long polling (hash code middleware) and the controllers.
  - Anonymous requests can no longer create queues or hold long-polling connections.
  - When authentication is enabled, `/info` and the Swagger UI also require credentials. Only `/health` and `/alive` stay anonymous.
- **Roles**: clients can have the roles `read`, `write` or `admin` (hierarchical).
  - Clients without roles get `admin` (backward compatible).
  - Insufficient roles return `403`.
- **Hardened basic authentication**:
  - Invalid Base64/UTF-8 headers return `401` instead of `500`.
  - Secrets are compared in constant time.
  - Clients are loaded once at startup.
  - Secrets can be stored as SHA-256 hash (`"Secret": "sha256:<hex>"`).
  - Invalid client configurations stop the startup.
- **Mutating endpoints no longer accept `GET`**:
  - `dequeue` and `confirmdequeue` use `POST`.
  - `remove` and `deletemessage` use `DELETE`.
  - `register` uses `PUT`.
- **Limits** (`MessageQueue:Limits`):
  - `MaxQueues` (10000)
  - `MaxMessageBytes` (1 MB)
  - `MaxDequeueCount` (1000)
  - `MaxQueueLength` (unlimited)
- **Error messages**: only expected errors return their message. Unexpected exceptions return `Internal server error` and are logged.
- **Startup warnings** if authentication is disabled, `Authorization:Type` is unknown, or no HTTPS URL is configured.
- **Regex DoS**: queue patterns collapse multiple wildcards and use a match timeout.
- **Long polling**:
  - Polling no longer creates queues; a non-existent queue has hash code `0`.
  - Polling stops when the client disconnects.
  - Invalid `MaxRequestPollingSeconds` values no longer crash requests.

### Breaking Changes

- HTTP methods changed:
  - `dequeue` and `confirmdequeue`: `POST`
  - `remove` and `deletemessage`: `DELETE`
  - `register`: `PUT`
- Clients older than 4.0 (`MessageQueueNET.Client`) cannot use these endpoints anymore. Update the client library together with the server.
- Queue names starting with `_` or `-`, or containing `..`, or ending with `.` are invalid. Persisted queues with such names are skipped on startup (a warning is logged, the files are not deleted).
- `confirmdequeue` and `deletemessage` no longer create missing queues; they return an error instead.
- With authentication enabled, `/info` and Swagger require credentials.

### Fixes

- `ItemLifetimeSeconds` is calculated from the original creation date, also for messages that were re-enqueued after a missing confirmation.
  - The confirmation period starts at dequeue time; persisted unconfirmed items get a new `DequeuedUTC` field.
  - Items persisted by older versions fall back to the creation date.
- Thread safety of the queue operations (enqueue, dequeue, delete, clear).
- Expired messages are no longer returned or moved to the unconfirmed list on dequeue.
- `MaxQueueLength` is checked before messages are persisted.
- `register` waits until the queue properties are persisted and reports errors.
- `all`, `length` and `remove` report errors instead of returning an empty success result.
- The hash code of a single queue is calculated the same way as for queue lists (`LastModifiedUTC`).
- The background maintenance service no longer blocks threads (`.Result`) and stops immediately on shutdown; errors are logged instead of swallowed.
- Queues are restored asynchronously before the application starts; invalid queues are logged and skipped instead of stopping the restore.
- Removed the unused `Program_old.cs`.

### Tests

- New test project `test/MessageQueueNET.Tests` (xUnit + `WebApplicationFactory`). It covers queue name validation, path traversal, authentication, roles, HTTP methods, limits, and the enqueue/dequeue/confirm roundtrip.
