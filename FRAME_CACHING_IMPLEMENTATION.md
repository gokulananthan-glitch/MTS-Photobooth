# Frame Template Caching and Display Implementation

## Overview
This document describes the implementation of frame template fetching, caching, and display functionality in the photobooth application. The system follows the same offline/sync pattern as the machine configuration.

## Features Implemented

### 1. Frame Template Model (`Models/FrameTemplate.cs`)
- **FrameTemplate**: Represents a frame template from the API
  - `Id`: Unique identifier
  - `MachineCode`: Machine code this frame belongs to
  - `Frame`: Frame name (e.g., "grid2", "grid4")
  - `Status`: Frame status ("active" or "inactive")
  - `Image`: Base64 encoded image or URL
  - `SiteCode`: Site code
  - `CreatedAt`, `UpdatedAt`: Timestamps
  
- **FrameTemplatesResponse**: API response wrapper

### 2. Database Service Updates (`Services/DatabaseService.cs`)
Added SQLite table and methods for frame template caching:

#### New Table: `FrameTemplates`
```sql
CREATE TABLE IF NOT EXISTS FrameTemplates (
    Id TEXT PRIMARY KEY,
    MachineCode TEXT NOT NULL,
    Frame TEXT NOT NULL,
    Status TEXT NOT NULL,
    Image TEXT NOT NULL,
    SiteCode TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    SavedAt TEXT NOT NULL
);
```

#### New Methods:
- `SaveFrameTemplatesAsync(List<FrameTemplate>)`: Saves frame templates to database with timestamp
- `GetFrameTemplatesAsync()`: Retrieves active frame templates from database
- `GetFramesSavedTimestampAsync()`: Gets the timestamp when frames were last saved

### 3. API Service Updates (`Services/ApiService.cs`)
Added method to fetch frame templates from API:

- `GetFrameTemplatesAsync(string machineCode)`: Fetches frames from `/api/machine-frames/all-frames/{machineCode}?status=active`

### 4. StartPage Updates (`Pages/StartPage.xaml.cs`)
Enhanced to fetch and cache frame templates during sync:

- When syncing configuration (30-minute check or manual sync), also fetches and saves frame templates
- Ensures frames are available offline

### 5. FrameSelectionPage Complete Redesign

#### UI Updates (`Pages/FrameSelectionPage.xaml`)
- **Modern, Responsive Layout**:
  - Gradient background with cyan/blue theme
  - Title with glow effect
  - Scrollable content area
  - Two main sections:
    1. **Frame Preview Section**: Displays the final frame with overlay
    2. **Captured Images Section**: Shows all captured photos in a responsive grid
  - Loading overlay with horizontal animated loader
  - Retry and Print buttons with hover effects

#### Code-Behind Updates (`Pages/FrameSelectionPage.xaml.cs`)
- **Frame Template Loading**:
  - Checks offline mode from `App.CurrentMachineConfig`
  - If offline mode is enabled, uses cached frames
  - If 30 minutes have passed, attempts background sync
  - Falls back to cached data if API fails
  
- **Frame Generation and Overlay**:
  - Generates base frame with captured images using `FrameGenerator`
  - Loads frame template from API (base64 or URL)
  - Composites frame template over generated image
  - Maps style numbers to frame names (grid1, grid2, grid4, grid6, grid9)
  
- **Captured Images Display**:
  - Shows all captured photos in a responsive grid below the frame
  - Each image has a border and glow effect

### 6. Image Processor Updates (`Utils/ImageProcessor.cs`)
Added new method for compositing images:

- `CompositeFrameOverImage(BitmapSource baseImage, BitmapSource frameOverlay)`: 
  - Uses `DrawingVisual` to layer frame template over base image
  - Returns a composited `BitmapImage`
  - Handles transparency and alpha blending

## Data Flow

### Initial Load (Online Mode)
1. User clicks START on StartPage
2. App fetches machine config from API
3. App fetches frame templates from API
4. Both are saved to SQLite database with timestamp
5. User proceeds through the flow

### Subsequent Loads (Offline Mode)
1. User clicks START on StartPage
2. App checks `offline_mode` flag in saved config
3. If true, uses cached config and frames
4. If 30 minutes have passed, prompts user to sync
5. If user syncs, fetches latest config and frames

### Frame Selection Page
1. Loads frame templates from database (or API if online)
2. Displays captured images in a grid
3. Generates base frame with captured images
4. Finds matching frame template based on selected style
5. Composites frame template over base image
6. Displays final result
7. Saves composite for printing

## Style to Frame Mapping

The system maps style numbers to grid types:
- **Style 1** → grid1 (1 photo)
- **Style 2** → grid2 (2 photos)
- **Style 3** → grid4 (4 photos)
- **Style 4** → grid6 (6 photos, horizontal)
- **Style 5** → grid9 (4 photos)

Frame templates from the API should use these grid names in the `frame` field.

## API Integration

### Endpoint
```
GET /api/machine-frames/all-frames/{machine_code}?status=active
```

### Expected Response
```json
{
  "statusCode": 200,
  "message": "Records fetched successfully",
  "data": [
    {
      "_id": "507f1f77bcf86cd799439011",
      "machine_code": "M100",
      "frame": "grid2",
      "status": "active",
      "image": "data:image/png;base64,iVBORw0KG...",
      "site_code": "9000",
      "createdAt": "2024-01-15T10:30:00.000Z",
      "updatedAt": "2024-01-15T10:30:00.000Z"
    }
  ]
}
```

### Image Format
- **Base64**: `data:image/png;base64,{base64_data}` or just the base64 string
- **URL**: `https://example.com/frame.png`

## Offline Mode Behavior

1. **First Run**: Must be online to fetch config and frames
2. **Subsequent Runs**: 
   - If `offline_mode: true`, uses cached data
   - If `offline_mode: false`, fetches fresh data
3. **30-Minute Sync**: 
   - Checks timestamp of last save
   - If > 30 minutes, prompts user to sync
   - Syncs both config and frames together

## Error Handling

- If API fails, falls back to cached data
- If no cached data exists, shows error message
- If frame template not found, displays generated frame without overlay
- If frame overlay fails, displays generated frame without overlay
- All errors are logged to debug console

## UI Enhancements

### FrameSelectionPage
- **Responsive Design**: Adapts to different screen sizes
- **Smooth Animations**: Fade-in effects for all elements
- **Visual Hierarchy**: Clear separation between frame and captured images
- **Modern Aesthetics**: Cyan/blue theme with glows and shadows
- **Loading State**: Full-screen overlay with animated loader
- **User Feedback**: Clear error messages and fallbacks

## Files Modified

1. `Models/FrameTemplate.cs` (NEW)
2. `Services/DatabaseService.cs` (UPDATED)
3. `Services/ApiService.cs` (UPDATED)
4. `Pages/StartPage.xaml.cs` (UPDATED)
5. `Pages/FrameSelectionPage.xaml` (REDESIGNED)
6. `Pages/FrameSelectionPage.xaml.cs` (REDESIGNED)
7. `Utils/ImageProcessor.cs` (UPDATED)

## Testing Checklist

- [ ] Verify frame templates are fetched on first run
- [ ] Verify frame templates are saved to database
- [ ] Verify offline mode uses cached frames
- [ ] Verify 30-minute sync prompt appears
- [ ] Verify sync updates both config and frames
- [ ] Verify frame overlay works with base64 images
- [ ] Verify frame overlay works with URL images
- [ ] Verify fallback to generated frame if template not found
- [ ] Verify captured images display correctly
- [ ] Verify responsive layout on different screen sizes
- [ ] Verify print functionality with composited frame

## Future Enhancements

1. **Frame Selection UI**: Allow users to choose from multiple frame templates
2. **Frame Preview**: Show frame templates before capture
3. **Custom Frames**: Allow users to upload custom frames
4. **Frame Categories**: Organize frames by theme/category
5. **Frame Rotation**: Support different orientations
6. **Frame Effects**: Add filters and effects to frames

