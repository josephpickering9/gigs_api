# Google Calendar Setup Guide (Service Account)

This guide will help you set up Google Calendar integration using a service account for the Gigs API, allowing you to import past calendar events as gigs.

## Prerequisites

- A Google account with calendar events
- Calendar events with **location field** filled in matching your existing venues
- Access to [Google Cloud Console](https://console.cloud.google.com/)

## Step 1: Create a Google Cloud Project

1. Go to the [Google Cloud Console](https://console.cloud.google.com/)
2. Click on the project dropdown at the top of the page
3. Click **New Project**
4. Enter a project name (e.g., "Gigs Calendar Integration")
5. Click **Create**

## Step 2: Enable Google Calendar API

1. In the Google Cloud Console, select your newly created project
2. Navigate to **APIs & Services** > **Library**
3. Search for "Google Calendar API"
4. Click on **Google Calendar API** in the results
5. Click **Enable**

## Step 3: Create Service Account (Optional)

> [!TIP]
> If you already have a service account for VertexAI, **you can skip this step and Step 4**! Just reuse your existing service account by sharing your calendar with it (Step 5).

If you want a separate service account for calendar:

1. Navigate to **APIs & Services** > **Credentials**
2. Click **Create Credentials** > **Service Account**
3. Fill in the service account details:
   - **Service account name**: gigs-calendar (or your preferred name)
   - **Service account ID**: Will be auto-generated
   - **Description**: Service account for Gigs calendar import
4. Click **Create and Continue**
5. Skip the optional steps (no roles needed for accessing shared calendars)
6. Click **Done**

## Step 4: Create and Download Service Account Key (Optional)

> [!TIP]
> Skip this if you're reusing your VertexAI service account.

If you created a new service account in Step 3:

1. On the **Credentials** page, find your newly created service account
2. Click on the service account email
3. Go to the **Keys** tab
4. Click **Add Key** > **Create new key**
5. Select **JSON** as the key type
6. Click **Create**
7. The JSON key file will download automatically - **save this securely**

## Step 5: Share Your Google Calendar with Service Account

This is a critical step! You must share your calendar with the service account.

1. **Find your service account email**:
   - If using VertexAI credentials: Check your VertexAI JSON key file
   - If using separate calendar credentials: Check the downloaded JSON file
   - The email looks like: `gigs-calendar@project-name.iam.gserviceaccount.com`

2. **Open Google Calendar** (https://calendar.google.com)

3. **Share your calendar**:
   - Find your calendar in the left sidebar
   - Click the three dots next to it
   - Select **Settings and sharing**
   - Scroll to **Share with specific people**
   - Click **Add people**
   - Paste the service account email
   - Set permission to **"See all event details"**
   - Click **Send**

> [!IMPORTANT]
> Without this step, the service account won't be able to access your calendar events!

> [!TIP]
> You can use the same service account you created for VertexAI - just grant it calendar access. No need to create a new one!

## Step 6: Configure Environment Variables

**Good news!** The calendar integration automatically reuses your existing **VertexAI service account credentials**. You don't need to set up separate credentials.

### Default Configuration (Using VertexAI Credentials)

Since you already have VertexAI configured, just add the calendar ID to your `.env`:

```env
GoogleCalendarId=primary
```

That's it! The service will use your existing `VertexAiCredentialsFile` or `VertexAiCredentialsJson`.

### Alternative: Separate Credentials (Optional)

If you want to use a different service account for calendar access, you can override with calendar-specific credentials:

**Option A: Using JSON String**
```env
GoogleCalendarCredentialsJson={"type":"service_account",...}
GoogleCalendarId=primary
```

**Option B: Using File Path**
```env
GoogleCalendarCredentialsFile=/path/to/calendar-key.json
GoogleCalendarId=primary
```

> [!NOTE]
> Calendar-specific credentials take precedence if set. Otherwise, it falls back to VertexAI credentials.

## Step 7: Ensure Venues Exist in Database

The calendar import only creates gigs for events where the **location matches an existing venue** in your database.

Before importing:
1. Make sure all your venues are already in the database
2. Calendar event locations should match venue names exactly (or "Venue Name, City" format)

Example:
- **Calendar Event Location**: `O2 Academy Brixton, London`
- **Database Venue**: Name = `O2 Academy Brixton`, City = `London` ✅

## Step 8: Build and Run

1. Restore NuGet packages:
   ```bash
   cd /Users/josephpickering/Documents/projects/gigs/gigs_api/Gigs
   dotnet restore
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

3. Run the application:
   ```bash
   dotnet run
   ```

## Using the Calendar Integration

### 1. Check Events (Preview)

View calendar events without importing:

```bash
curl "http://localhost:5105/api/calendar/events?startDate=2020-01-01&endDate=2025-12-31"
```

This shows you which events will be imported and helps verify the location matching.

### 2. Import Calendar Events

Import events as gigs:

```bash
curl -X POST http://localhost:5105/api/calendar/import \
  -H "Content-Type: application/json" \
  -d '{"startDate": "2020-01-01T00:00:00Z", "endDate": "2025-12-31T23:59:59Z"}'
```

Response:
```json
{
  "eventsFound": 150,
  "gigsCreated": 142,
  "gigsUpdated": 8,
  "message": "Successfully processed 150 events: 142 created, 8 updated"
}
```

### 3. Verify Gigs Created

Check your database or use existing gig endpoints to verify:
- Gigs were created for matching venues
- Events without matching venues were skipped
- No duplicates were created

## Calendar Event Formatting Tips

### Event Title

The title becomes the artist name (headliner):
- Simple: `The Beatles`
- With venue: `The Beatles @ Abbey Road` (venue part is auto-removed)

### Event Location ⭐ **CRITICAL**

The location **must match an existing venue** in your database:
- Exact match: `Abbey Road Studios`
- With city: `Abbey Road Studios, London`
- With address: `Abbey Road Studios, 3 Abbey Road, London`

Matching is flexible:
1. Tries exact match of full location string
2. Tries first part (before comma) as venue name
3. Tries first part + last part as venue name + city

### Event Description (Optional)

Include additional information:
- **Support acts**: `Support: Arctic Monkeys, The Strokes`
- **Ticket cost**: `£45.00` or `$30.00`

Example:
```
Support: Band A, Band B
Ticket: £25.00
```

## Troubleshooting

### "No events imported" or Low Import Count

**Problem**: Many calendar events but few/no gigs created

**Likely Cause**: Event locations don't match existing venues

**Solution**:
1. Use the `/api/calendar/events` endpoint to preview events
2. Check the `location` field in the response
3. Ensure those locations match venue names in your database
4. Update either calendar event locations or venue names to match

### "Error: credentials not configured"

**Problem**: Application can't find service account credentials

**Solution**:
- Verify `.env` file has either `GoogleCalendarCredentialsFile` or `GoogleCalendarCredentialsJson`
- Check file path is absolute and file exists
- Ensure JSON string is valid (no line breaks in `.env`)

### "Error: The caller does not have permission"

**Problem**: Service account can't access calendar

**Solution**:
- Verify you've shared your calendar with the service account email
- Check permission is set to "See all event details" (not just "See only free/busy")
- Wait a few minutes after sharing (can take time to propagate)

### Duplicate Gigs Created

**Problem**: Re-running import creates duplicate gigs

**Should Not Happen**: The system checks for existing gigs by date + venue

**If it happens**:
- Check if venue matching is inconsistent (e.g., "Venue Name" vs "Venue Name, City")
- Verify database constraints

## Security Notes

- **Never commit the JSON key file to version control**
- Add key file to `.gitignore`
- Store credentials securely in production (environment variables, secrets manager)
- Service account only needs calendar read access
- Regularly rotate service account keys (every 90 days recommended)

## Production Deployment

For Digital Ocean droplet deployment:

1. **Add secrets to GitHub** (if using GitHub Actions):
   - `GOOGLE_CALENDAR_CREDENTIALS_JSON`: Full JSON key content

2. **Set environment variable** on the droplet:
   ```bash
   export GoogleCalendarCredentialsJson='{"type":"service_account",...}'
   ```

3. **Or use a secure file**:
   - Upload key file to secure location on server
   - Set `GoogleCalendarCredentialsFile` environment variable
   - Ensure file permissions are restrictive (`chmod 600`)

## Differences from OAuth 2.0

| Feature | Service Account | OAuth 2.0 |
|---------|----------------|-----------|
| Setup complexity | Low | High |
| User interaction | None | Required |
| Calendar sharing | Manual (one-time) | Automatic |
| Token management | N/A (uses key) | Session-based |
| Multi-user | Not ideal | Built for it |
| Maintenance | Low | Medium |

Service accounts are perfect for single-user scenarios where you control the calendar!
