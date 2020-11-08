# Acid Rain for Rust

Unprotected exposure to rain in Rust now causes radiation damage!

New spawns are protected by the protectionTimer, which defaults to 5 minutes (300 seconds).

After that, various articles of clothing will provide protection from the acid rain.  However, the slightest bit of wetness can lead to exposure.  So, choose your garments well.

### Configuration
``` json
{
  "Options": {
    "hilevelbump": 1.0,
    "hipoisonbump": 0.5,
    "lolevelbump": 0.2,
    "lopoisonbump": 0.1,
    "notifyTimer": 60.0,
    "protectionTimer": 300.0
  },
  "Version": {
    "Major": 1,
    "Minor": 0,
    "Patch": 1
  }
}
```

- `hilevelbump` -- How much to increase the player's radiation level when rain > 0.5
- `hipoisonbump` -- How much to increase the player's radiation poision level when rain > 0.5
- `lolevelbump` -- How much to increase the player's radiation level when rain < 0.5
- `lopoisonbump` -- How much to increase the player's radiation poision level when rain < 0.5
- `notifyTimer` -- The player will be notified once while taking damage until this timer expires.
- `protectionTimer` -- How long will fresh spawns be protected from the acid rain.


### Future plans, maybe
- Clothing protection customization
- ???
