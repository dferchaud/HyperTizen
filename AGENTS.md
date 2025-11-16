# AGENTS.md - Guide for AI Assistants Working with HyperTizen

This document provides essential guidance for Claude or other LLMs working with the HyperTizen codebase. Read this first before making any changes.

---

## Quick Start for AI Assistants

### Project Overview

HyperTizen is an experimental fork of a Hyperion/HyperHDR screen capturer for Samsung Tizen TVs, focused on **Tizen 8.0+ screen capture research**.

**Current Status:**
- **T8 SDK Capture Method** = NOT YET IMPLEMENTED (scaffolding exists)
- **T7 SDK Capture Method** = NOT YET IMPLEMENTED (scaffolding exists)
- **Pixel Sampling Capture Method** = NOT YET IMPLEMENTED (scaffolding exists)

**Critical Constraint:** Always verify methods on actual TV hardware - emulator testing is not reliable. Different Tizen firmware versions may have different API availability.

---

## CRITICAL: Essential Files to Read First

Before making ANY changes, read these files IN THIS ORDER:

1. **`/home/user/HyperTizen/README.md`** - MOST CRITICAL
   - Current project status and VideoEnhance breakthrough (WORKING on Tizen 8+)
   - WebSocket log viewer usage (essential for debugging)
   - What works vs what's blocked
   - Installation and build instructions

2. **`/home/user/HyperTizen/.agents`**
   - Specialized agent definitions for different tasks
   - Key files for each area of expertise
   - Common workflows and troubleshooting

3. **Source Code** (as needed):
   - Review capture method implementations in `HyperTizen/Capture/`
   - Study native interop patterns in `HyperTizen/SDK/`
   - Analyze error handling in `HyperTizen/Helper/`

---

## Key Architecture

### Technology Stack
- **Language:** C# (.NET on Tizen)
- **Platform:** Samsung Tizen 8.0+ TV firmware
- **Interop:** P/Invoke for native library access (ARM Cdecl convention)
- **Debugging:** WebSocket server on port 45678 serving real-time logs

### Capture Strategy Architecture

HyperTizen uses a **systematic fallback architecture** with the `ICaptureMethod` interface:

```
Startup Flow:
1. CaptureMethodSelector tests methods in priority order
2. First working method is selected and initialized
3. Failed methods are automatically cleaned up
4. Single active capture method used for entire session

Three Capture Methods (Priority Order):
1. T8SdkCaptureMethod
   ├─ libvideo-capture.so.0.1.0
   ├─ Vtable implementation (Lock → getVideoMainYUV → Unlock)
   ├─ May not be available on all firmware versions
   └─ See: HyperTizen/Capture/T8SdkCaptureMethod.cs

2. T7SdkCaptureMethod
   ├─ libsec-video-capture.so.0
   ├─ Legacy API from Tizen 7.0 and earlier
   ├─ May not exist on Tizen 8.0+ firmware
   └─ See: HyperTizen/Capture/T7SdkCaptureMethod.cs

3. PixelSamplingCaptureMethod
   ├─ libvideoenhance.so
   ├─ VideoEnhance_SamplePixel() - samples individual RGB pixels
   ├─ Slower than frame capture, may have different availability
   └─ See: HyperTizen/Capture/PixelSamplingCaptureMethod.cs
```

**10-Step Service Startup:**
1. Service startup (initialize lifecycle management)
2. Start WebSocket logging server
3. Perform SSDP network scan
4. Diagnostic mode (if enabled via Preferences)
5. Test each capture method (CaptureMethodSelector)
6. Clean up failed methods
7. Initialize best working method
8. Start capture loop
9. Initiate FlatBuffers connection (send frames)
10. Continue until stopped

### Key Components

**Capture Architecture:**
- **`HyperTizen/Capture/ICaptureMethod.cs`** - Interface for all capture methods
- **`HyperTizen/Capture/CaptureMethodSelector.cs`** - Tests and selects best method
- **`HyperTizen/Capture/CaptureResult.cs`** - Standardized capture result wrapper
- **`HyperTizen/Capture/T8SdkCaptureMethod.cs`** - T8 API (implementation complete)
- **`HyperTizen/Capture/T7SdkCaptureMethod.cs`** - T7 legacy API (missing on T8+)
- **`HyperTizen/Capture/PixelSamplingCaptureMethod.cs`** - Pixel sampling approach

**Core Services:**
- **`HyperTizen/LogWebSocketServer.cs`** - WebSocket log streaming
- **`HyperTizen/HyperionClient.cs`** - Main application loop + 10-step startup
- **`HyperTizen/Preferences.cs`** - Runtime configuration (includes DIAGNOSTIC_MODE)
- **`logs.html`** - Browser-based real-time log viewer

### WebSocket Logging Architecture

**Port:** 45678
**URL:** `http://<TV_IP>:45678`

The WebSocket server streams all log messages in real-time to any connected browser. This is **essential for debugging** on TV hardware where console access is limited.

**Features:**
- Real-time log streaming
- Auto-reconnect with exponential backoff
- Color-coded log levels
- No TV SSH access required

### Diagnostic Mode

Runtime-configurable via `Preferences.DiagnosticMode`:
- Pauses app for 10 minutes after initialization
- Countdown every 60 seconds via TV notifications
- Allows testing capture methods via WebSocket logs
- Use this to test changes without app auto-connecting to Hyperion
- Toggle via preferences UI (no recompilation needed)

---

## Common Tasks & How to Approach Them

### Adding New Capture Methods

**Workflow:**
1. Read README.md for known working/blocked methods
2. Design safe P/Invoke declarations with ARM Cdecl convention
3. Implement dlopen/dlsym validation before use
4. Add proper error handling and logging
5. Test on actual TV hardware via WebSocket logs
6. Document in README.md

**Key files:**
- `references/GetCaptureFromTZ.c` - Decompiled Samsung code showing working implementation
- `references/2.webp` - Library export screenshot

**Safety rules:**
- Always use dlopen before DllImport to avoid crashes
- Check for known blacklisted libraries (graphics libraries, Wayland, etc.)
- Use timeouts on filesystem searches (app crashes easily)
- Test with small allocations first
- Never assume API works - verify on hardware

### Debugging Crashes

**Workflow:**
1. Review WebSocket logs at `http://<TV_IP>:45678`
2. Analyze error codes (see reference below)
3. Check README.md for known firmware blocks and issues
4. Verify struct layouts and marshaling
5. Test with diagnostic mode enabled

**Key files:**
- `logs.html` - Real-time log viewer
- `HyperTizen/Helper/Log.cs` - Logging infrastructure
- `README.md` - Known issues and solutions

**Common crash causes:**
- Unsafe library loading without dlopen/dlsym validation
- Loading blacklisted libraries (libgfx-*, libwayland*, etc.)
- Incorrect struct layouts causing memory corruption
- Missing error handling on native calls

### P/Invoke and Native Interop Work

**ARM Cdecl Convention:**
```csharp
[DllImport("library.so", CallingConvention = CallingConvention.Cdecl)]
private static extern int FunctionName(IntPtr param);
```

**Struct Layout Example:**
```csharp
[StructLayout(LayoutKind.Sequential)]
public struct InputParams
{
    public int field0;
    public int field1;
    public int field2;  // Use 0xffff for screen capture
    public int field3;  // Use 0xffff for screen capture
    public int field4;  // Use 1
    public int bufferSize1;  // 0x7e900 for standard
    public int bufferSize2;  // 0x7e900 for standard
    public IntPtr pYBuffer;   // Pointer to Y buffer
    public IntPtr pUVBuffer;  // Pointer to UV buffer
    // ... additional fields
}
```

**Safe Library Probing:**
```csharp
const int RTLD_LAZY = 1;
IntPtr handle = dlopen("/usr/lib/library.so", RTLD_LAZY);
if (handle != IntPtr.Zero)
{
    IntPtr symbol = dlsym(handle, "functionName");
    if (symbol != IntPtr.Zero)
    {
        // Symbol exists, safe to use
    }
    dlclose(handle);
}
```

**Key rules:**
- Always use `CallingConvention.Cdecl` for ARM Tizen
- Match struct sizes from decompiled code (check with sizeof)
- Use `IntPtr` for pointers, not direct references
- Wrap native calls in try-catch
- Call dlerror() after failures for diagnostics

### Documentation Updates

**When to update README.md and AGENTS.md:**
- At the END of every session (recommended)
- When discovering new blockers or crashes
- After trying approaches that failed
- When changing file structure or key implementations

**What to include:**
- Current Status (what works, what's broken RIGHT NOW)
- What was tried and what failed
- Files modified with line numbers
- Immediate next steps for the next session
- Any crash causes or gotchas

**Never:**
- Be optimistic about blocked features
- Leave assumptions undocumented
- Skip documenting failures (critical info!)
- Forget to sync code changes with docs

**Key files:**
- `README.md` - Project status and user guide (read first!)
- `AGENTS.md` - Session continuity and AI assistant guidance
- Technical .md files - Deep dive documentation

---

## Critical Constraints

### API Availability on Different Firmware Versions

**Observation:** Different capture APIs may not be available on all Tizen firmware versions.

**Considerations:**
- Some capture APIs return `-95 (EOPNOTSUPP)` on certain firmware versions
- The same implementation may behave differently on different Tizen versions
- DRM content detection returns different error codes (`-4`)

**Approach:**
- Test multiple capture methods to find what works on your specific firmware
- Different methods may work on different hardware/firmware combinations
- Document which methods work on which firmware versions

### DRM Content Restrictions

**Error code:** `-4`
**Cause:** DRM-protected content (Netflix, HDCP-protected HDMI, Widevine)
**Workaround:** None - DRM content cannot be captured (by design)

### Library Crash Risks

Many Tizen libraries crash when loaded due to undefined symbols or initialization requirements.

**Known blacklist** (libraries that commonly crash):
- `libgfx-video-output` - Undefined symbol crashes
- `libgfx-*` - Graphics libs unstable
- `libscreen_connector` - Undefined symbols
- `libwayland*` - Wayland graphics issues
- `libgl*`, `libegl*`, `libvulkan*` - Graphics drivers

**Prevention:**
- Always use dlopen/dlsym before DllImport
- Check blacklist before loading
- Use timeouts on searches (filesystem operations can hang/crash)
- Test on actual TV, not emulator

### Hardware Testing Requirements

**Emulator limitations:**
- Different library versions
- Missing Samsung-specific libraries
- Different firmware feature flags
- Cannot test actual capture behavior

**Always test on:**
- Actual Samsung TV running Tizen 8.0+
- Via WebSocket logs (`http://<TV_IP>:45678`)
- With diagnostic mode enabled for extended testing

---

## When to Use Which Agent

The `.agents` file defines specialized agents for different aspects of the project. Reference that file for detailed expertise areas.

### Quick Agent Reference

**@tizen-capture-expert**
- Capture API questions and implementations
- Error code interpretation
- VTable analysis and native library internals
- Performance optimization
- *When:* Working on capture methods, analyzing APIs, debugging capture failures

**@debugging-assistant**
- WebSocket log analysis
- Error code diagnosis
- Diagnostic mode guidance
- Performance profiling
- *When:* App crashes, analyzing logs, remote debugging on TV

**@native-interop-expert**
- P/Invoke declarations
- Struct layout and marshaling
- dlopen/dlsym symbol probing
- ARM calling conventions
- *When:* Writing native interop, debugging marshaling, memory corruption issues

**@documentation-writer**
- Updating README.md and AGENTS.md
- Technical documentation
- API documentation
- Troubleshooting guides
- *When:* End of session, documenting discoveries, updating docs

### Common Workflows

**Testing New Capture Method:**
```
1. @native-interop-expert - Write safe P/Invoke declarations
2. @tizen-capture-expert - Implement capture logic
3. @debugging-assistant - Test via WebSocket logs
4. @documentation-writer - Document the method
```

**Debugging Crash:**
```
1. @debugging-assistant - Analyze WebSocket logs
2. @tizen-capture-expert - Interpret error codes
3. @native-interop-expert - Check marshaling/struct layouts
```

**End of Session:**
```
1. @documentation-writer - Update README.md and AGENTS.md
2. Document any new discoveries
3. Note crashes, blockers, gotchas for next session
```

---

## Development Workflow

### Standard Workflow for Any Task

1. **Read README.md FIRST**
   - Understand current status
   - Check for known blockers
   - Review recent changes
   - Note any crash causes

2. **Make changes with proper error handling**
   - Wrap native calls in try-catch
   - Add detailed logging
   - Check blacklists before library loading
   - Use dlopen for safe probing

3. **Test via WebSocket logs**
   - Open `http://<TV_IP>:45678` in browser
   - Watch real-time logs during testing
   - Enable diagnostic mode for extended testing
   - Document all results

4. **Update README.md and AGENTS.md before finishing session**
   - What worked vs what failed
   - Files modified (with line numbers)
   - Next steps for continuation
   - Any gotchas or crashes discovered

5. **Never assume APIs work**
   - Always verify on actual TV hardware
   - Check error codes in logs
   - Test with DRM and non-DRM content
   - Document firmware version tested

### Diagnostic Mode Testing

**Enable via:** Preferences UI or `Preferences.DiagnosticMode = true`

**Behavior:**
- App pauses for 10 minutes after initialization
- Countdown notifications every 60 seconds
- Allows testing capture without Hyperion connection
- Watch WebSocket logs for detailed output
- Runtime configurable (no recompilation needed)

**Use for:**
- Testing new capture implementations
- Analyzing error codes over time
- Monitoring performance metrics
- Debugging without auto-connect interference

---

## Error Code Reference

### Common Error Codes

| Code | Meaning | Cause | Action |
|------|---------|-------|--------|
| **0** | Success | Normal operation | Continue |
| **4** | Success (alternate) | Valid result code | Continue |
| **-4** | DRM Content | Netflix, HDCP, Widevine protected | Cannot capture - expected |
| **-95** | Operation Not Supported | API may not be available on this firmware | Try alternative methods |
| **-99** | Not Initialized | Library not ready or internal error | Check initialization |

### Error Code Details

**-95 (EOPNOTSUPP) - Operation Not Supported**
- **Cause:** The API may not be available on this firmware version
- **Note:** T8 vtable API implementation is correct but returns this error on some firmware
- **Approach:** Test alternative capture methods
- **Testing:** Verify behavior on actual hardware (may vary by firmware/TV model)
- **Applies to:** Various capture APIs on different Tizen versions

**-4 (DRM Protected)**
- **Cause:** Content is DRM-protected (Netflix, HDCP HDMI, Widevine)
- **Workaround:** None - DRM protection is working as intended
- **Testing:** Use non-DRM content (regular HDMI input, built-in apps)
- **Expected:** Some content will always return this error

**-99 (Not Initialized)**
- **Cause:** Library not initialized or internal failure
- **Check:** getInstance() call succeeded
- **Check:** Lock/Unlock sequence correct
- **Check:** Buffer allocations valid

**0 or 4 (Success)**
- **Validation:** Check width > 960 and height > 540
- **Validation:** Verify buffer contains actual pixel data
- **Note:** Success code varies by API version

### Checking Results in Code

```csharp
// From decompiled Samsung code
int result = getVideoMainYUV(instance, inputParams, outputParams);

// Success check: result is 0 or 4
if ((result + 4U & 0xfffffffb) == 0)
{
    // Success - check resolution
    if (outputParams.width > 960 && outputParams.height > 540)
    {
        // Valid capture
    }
}
```

---

## Files NOT to Modify

Unless explicitly required or fixing bugs, avoid modifying:

### Core Protocol Infrastructure
- **Flatbuffers protocol code** - Hyperion/HyperHDR communication
- **Networking layer** - Connection management
- **WebSocket infrastructure** - Unless debugging WebSocket issues specifically

### Build Configuration
- **`.csproj` files** - Unless adding new dependencies
- **`tizen-manifest.xml`** - Unless changing permissions/capabilities
- **Certificate files** - Build/signing configuration

### Capture Method Implementations
- **Capture method files** - Test thoroughly before modifying
- **HyperTizen/Capture/*.cs** - Core capture implementations

### What You CAN Modify

- **Capture method implementations** - T8 API, T7 API, Pixel Sampling, or new methods (test thoroughly)
- **Log.cs** - Adding logging features
- **Preferences.cs** - Adding new configuration options
- **Documentation files** - Always keep these updated

---

## Session Handoff Protocol

This is important for continuity between development sessions.

### At Start of Session

1. **Read README.md FIRST**
2. Check current status and blockers
3. Review AGENTS.md for workflow guidance
4. Note any crash causes or gotchas
5. Understand immediate next steps

### During Session

1. **Take notes** on what you try
2. **Document failures** (critical for next session)
3. **Track file changes** with line numbers
4. **Note any crashes** and what caused them
5. **Record error codes** and their contexts

### At End of Session

1. **Update README.md and AGENTS.md**
   - Current Status section in README.md
   - What was tried and what failed
   - Files modified with line numbers
   - Next steps for continuation
   - Any crash causes discovered

2. **Update code comments and README**
   - Add inline comments for any new discoveries
   - Update README.md with new findings or blockers

3. **Be honest and factual**
   - Don't be optimistic about blocked features
   - Clearly state what doesn't work
   - Document firmware restrictions
   - Note any assumptions made

### Handoff Best Practices

**DO:**
- State blockers clearly and directly
- Document every failed attempt
- Include specific error codes and logs
- Note files changed with line numbers
- List immediate actionable next steps

**DON'T:**
- Be optimistic about blocked features
- Skip documenting failures
- Assume next session will remember context
- Leave crashes undocumented
- Forget to note firmware restrictions

---

## Key Technical References

### Decompiled Code
**File:** `/home/user/HyperTizen/references/GetCaptureFromTZ.c`

Shows actual working Samsung implementation:
- getInstance() call pattern
- Lock/Unlock sequence (vtable[13], vtable[14])
- getVideoMainYUV call (vtable[3])
- Input parameter initialization
- Success validation logic

**Critical insights:**
- Exact struct field values (0xffff, 0x7e900, etc.)
- Buffer size calculations
- Resolution validation (width > 960, height > 540)
- Success check: `(result + 4U & 0xfffffffb) == 0`

### Library Exports
**File:** `/home/user/HyperTizen/references/2.webp`

Screenshot showing exported functions from `libvideo-capture-impl-sec.so`:
- `getVideoMainYUV` (plain C export, not mangled)
- `getVideoPostYUV` (plain C export)
- Other capture-related functions

**Note:** This library doesn't exist on user's Tizen 8.0 TV, but exports are useful for understanding API design.

### Current Documentation
- **README.md** - Project status and current state (read first!)
- **AGENTS.md** - AI assistant guidance and workflows
- **Source code comments** - Inline documentation in key files

---

## Quick Troubleshooting Guide

### App Crashes on Start

**Check:**
1. README.md and technical docs for known crash causes
2. Library scanning disabled/minimized (common crash source)
3. No blacklisted libraries being loaded
4. Timeout values on filesystem searches

**Common causes:**
- Too many filesystem operations in library search
- Loading blacklisted libraries (libgfx-*, etc.)
- Missing error handling on native calls
- Unsafe dlopen without symbol checking

### Error -95 (Operation Not Supported)

**Observation:** This API may not be available on the current firmware version.

**Actions:**
- Test alternative capture methods to find what works
- Document which methods work/don't work on your specific hardware
- This is expected behavior on some firmware versions
- Error code may vary by TV model and firmware version

### Error -4 (DRM Content)

**Reality:** DRM protection working as intended

**Actions:**
- Test with non-DRM content (regular HDMI)
- Don't try to bypass (impossible and illegal)
- Document in logs which content is DRM-protected
- This is expected behavior

### WebSocket Not Connecting

**Check:**
1. TV IP address correct
2. Port 45678 accessible on network
3. HyperTizen app actually running on TV
4. Firewall not blocking port
5. Browser on same network as TV

**Test:**
```bash
# From same network
curl http://<TV_IP>:45678
# Should return logs.html content
```

### Pixel Sampling Performance

**Observation:** Pixel sampling methods may be slower than frame capture approaches

**Actions:**
- Optimize batch sizes for pixel sampling
- Adjust sleep times between samples based on your firmware performance
- Performance may vary by TV model and firmware version
- Balance speed vs availability based on your hardware

### getInstance Symbol Not Found

**Check:**
1. SSH to TV: `nm -D /usr/lib/libvideo-capture.so.0.1.0 | grep -i instance`
2. Try mangled names: `_Z11getInstancev`, `_ZN13IVideoCapture11getInstanceEv`
3. Check if getInstance in different library
4. May need alternative initialization method

**Reference:**
- GetCaptureFromTZ.c line 56 for usage pattern
- README.md for current status

---

## Summary: Quick Reference

### Before You Start
1. ✅ Read README.md
2. ✅ Check current status and blockers
3. ✅ Review recent changes and crashes
4. ✅ Understand project constraints

### While Working
1. ✅ Use WebSocket logs for debugging
2. ✅ Test on actual TV hardware
3. ✅ Add proper error handling
4. ✅ Document as you go

### Before You Finish
1. ✅ Update README.md and AGENTS.md
2. ✅ Document failures (critical!)
3. ✅ List files changed with line numbers
4. ✅ Note next steps and blockers

### Key Constraints to Remember
- Multiple capture methods exist - test to find what works on your firmware
- T8 API returns -95 on some firmware versions (may vary by TV model)
- DRM content always returns -4 (expected behavior)
- Library crashes common - use blacklist
- Always test on actual TV hardware
- WebSocket logs are essential for debugging
- Different firmware versions may have different API availability

### File Paths Quick Reference
```
/home/user/HyperTizen/README.md  (READ FIRST!)
/home/user/HyperTizen/AGENTS.md
/home/user/HyperTizen/.agents
/home/user/HyperTizen/HyperTizen/Capture/ICaptureMethod.cs  (Capture interface)
/home/user/HyperTizen/HyperTizen/Capture/CaptureMethodSelector.cs  (Selection logic)
/home/user/HyperTizen/HyperTizen/Capture/PixelSamplingCaptureMethod.cs  (WORKING)
/home/user/HyperTizen/HyperTizen/Capture/T8SdkCaptureMethod.cs  (T8 - BLOCKED)
/home/user/HyperTizen/HyperTizen/HyperionClient.cs  (10-step startup flow)
/home/user/HyperTizen/logs.html  (WebSocket viewer)
/home/user/HyperTizen/references/GetCaptureFromTZ.c  (Decompiled reference)
```

---

## For More Information

- **Specialized workflows:** See `.agents` file for detailed agent definitions
- **Current status:** Always check README.md
- **Debugging:** Use WebSocket logs at `http://<TV_IP>:45678`
- **Questions:** Reference the appropriate agent (@tizen-capture-expert, etc.)
- **Source code:** Review inline comments and capture method implementations

---

**Remember:** This is research code exploring screen capture on Samsung Tizen TVs. Always be factual about what works and what doesn't on your specific hardware/firmware, document everything, and maintain continuity via README.md and AGENTS.md.
