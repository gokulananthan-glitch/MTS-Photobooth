# Print Folder Setup Guide

## 🖨️ Hot Printing Folder Configuration

The photobooth saves final photos to a "Hot Printing Folder" for automatic printing or backup.

---

## 📁 **Default Configuration**

Location in `appsettings.json`:
```json
{
  "PrintSettings": {
    "HotPrintingFolder": "C:\\PhotoBooth\\Prints"
  }
}
```

---

## ✅ **Recommended Folders**

### **Option 1: Local Drive (Recommended)**
```json
"HotPrintingFolder": "C:\\PhotoBooth\\Prints"
```
- ✅ Fast access
- ✅ Reliable
- ✅ Automatically created

### **Option 2: User Documents**
```json
"HotPrintingFolder": "C:\\Users\\YourUsername\\Documents\\PhotoBooth\\Prints"
```
- ✅ No permission issues
- ✅ Easy to find
- ✅ Backed up with user data

### **Option 3: Network Drive (For Printing Server)**
```json
"HotPrintingFolder": "\\\\PRINTSERVER\\HotPrinting"
```
- ⚠️ Requires network connection
- ⚠️ May be slower
- ✅ Good for centralized printing

### **Option 4: External Drive**
```json
"HotPrintingFolder": "D:\\PhotoBooth\\Prints"
```
- ✅ Large storage
- ✅ Easy backup
- ⚠️ Must be connected

---

## 🛠️ **Setup Instructions**

### **1. Choose Your Folder**
Decide where you want photos saved.

### **2. Edit appsettings.json**
```json
{
  "PrintSettings": {
    "HotPrintingFolder": "YOUR_CHOSEN_PATH"
  }
}
```

**Important:** Use double backslashes `\\` in paths!
- ✅ Correct: `"C:\\PhotoBooth\\Prints"`
- ❌ Wrong: `"C:\PhotoBooth\Prints"`

### **3. Test Permissions**
Manually create the folder and test:
1. Create folder: `C:\PhotoBooth\Prints`
2. Try to create a test file inside
3. If successful, you have proper permissions

---

## 🔧 **Automatic Fallback**

If the configured folder fails, the app automatically falls back to:
```
C:\Users\[YourUsername]\Documents\PhotoBooth\Prints
```

This ensures photos are always saved, even if there's a configuration issue.

---

## ❌ **Common Errors & Solutions**

### **Error: "Cannot create print folder"**

**Cause:** No permission to create folder in C:\ drive

**Solution:**
1. Run app as Administrator (right-click → Run as Administrator)
2. OR change to Documents folder:
   ```json
   "HotPrintingFolder": "C:\\Users\\YourUsername\\Documents\\PhotoBooth\\Prints"
   ```

### **Error: "Path is too long"**

**Cause:** Path exceeds 260 characters

**Solution:** Use a shorter path:
```json
"HotPrintingFolder": "C:\\Prints"
```

### **Error: "Access denied"**

**Cause:** Folder exists but no write permissions

**Solution:**
1. Right-click folder → Properties → Security
2. Add your user account with "Modify" permissions
3. OR choose a different folder you own

### **Error: "Network path not found"**

**Cause:** Network drive not accessible

**Solution:**
1. Check network connection
2. Verify server is running
3. Use local folder temporarily

---

## 📊 **File Naming Convention**

Saved files use this format:
```
PhotoBooth_YYYYMMDD_HHMMSS_UniqueID.jpg
```

Example:
```
PhotoBooth_20251224_143052_a7b3c1d4.jpg
```

- `20251224` = December 24, 2025
- `143052` = 2:30:52 PM
- `a7b3c1d4` = Unique ID (prevents overwrites)

---

## 🔍 **Monitoring & Logs**

### **Debug Logs Show:**
```
[PrintPage] Source file: C:\Temp\frame_composite_20251224_143052.png
[PrintPage] Source file size: 2458742 bytes
[PrintPage] Target folder: C:\PhotoBooth\Prints
[PrintPage] Created print folder: C:\PhotoBooth\Prints
[PrintPage] Destination: C:\PhotoBooth\Prints\PhotoBooth_20251224_143052_a7b3c1d4.jpg
[PrintPage] File copied successfully on attempt 1
[PrintPage] Saved file size: 2458742 bytes
[PrintPage] ✓ File saved successfully
```

### **On Error:**
```
[PrintPage] ✗ Error saving file:
Error Type: UnauthorizedAccessException
Message: Access to the path 'C:\HotPrinting' is denied.
```

---

## 🎯 **Best Practices**

### **For Events:**
1. ✅ Create folder BEFORE event
2. ✅ Test saving a file manually
3. ✅ Check disk space (>10GB recommended)
4. ✅ Use local drive (C:) for speed

### **For Production:**
1. ✅ Use dedicated folder
2. ✅ Regular backups
3. ✅ Monitor disk space
4. ✅ Consider network backup if available

### **For Testing:**
1. ✅ Use Documents folder
2. ✅ Easy to find and verify
3. ✅ No permission issues

---

## 🚀 **Quick Setup for Different Scenarios**

### **Scenario 1: Single PC with Local Printer**
```json
"HotPrintingFolder": "C:\\PhotoBooth\\Prints"
```
Printer watches this folder for new files.

### **Scenario 2: Network Printing**
```json
"HotPrintingFolder": "\\\\PRINTSERVER\\PhotoBoothQueue"
```
Network printer polls this shared folder.

### **Scenario 3: Manual Printing**
```json
"HotPrintingFolder": "C:\\Users\\EventStaff\\Desktop\\PrintQueue"
```
Staff manually prints from desktop folder.

### **Scenario 4: Just Backup (No Printing)**
```json
"HotPrintingFolder": "D:\\PhotoBoothBackup"
```
External drive for backup only.

---

## ✅ **Validation Checklist**

Before your event:

- [ ] Folder path configured in `appsettings.json`
- [ ] Folder exists (or app can create it)
- [ ] You have write permissions
- [ ] Sufficient disk space (>10GB)
- [ ] Test save successful
- [ ] Printer can access folder (if applicable)

---

## 🆘 **Emergency Fallback**

If all else fails, the app saves to:
```
C:\Users\[YourUsername]\Documents\PhotoBooth\Prints
```

This folder:
- ✅ Always accessible
- ✅ Automatically created
- ✅ No permission issues

Photos will be saved here and you can manually copy them to the printer later.

---

## 📞 **Support**

If you encounter persistent issues:

1. Check debug log for detailed error
2. Verify folder path in `appsettings.json`
3. Test folder permissions manually
4. Use fallback Documents folder temporarily
5. Run app as Administrator if needed

**Remember:** Photos are ALWAYS saved somewhere (fallback ensures this), even if the primary folder fails!

---

**Last Updated:** December 24, 2025
**Status:** ✅ **Enhanced with Smart Fallback**


