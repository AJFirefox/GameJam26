using System;
using UnityEngine;

/// <summary>
/// The player's weapons. Only one is held at a time: the held weapon's GameObject is on, the rest are off.
/// Put this on the Player. Fortune's Edge is found automatically; drag the other weapons' objects
/// (e.g. JackpotPivot, PistolPivot) into their slots as they get built. Empty slots show as Locked at the Altar.
/// </summary>
public class WeaponLoadout : MonoBehaviour
{
    [Serializable]
    public class WeaponSlot
    {
        public string name;
        [TextArea] public string description;
        [Tooltip("The weapon's object (the pivot with its weapon script). Empty = not built yet / locked.")]
        public GameObject weaponObject;
    }

    public WeaponSlot[] slots =
    {
        new WeaponSlot { name = "Fortune's Edge", description = "Fast broadsword. 3-hit combo, lunge on hit 3. Jackpot on hit 3 fires a cleave wave." },
        new WeaponSlot { name = "The Jackpot",    description = "Slow war hammer. Jackpots spin 3 god reels for chain lightning, shockwaves, lifesteal and more." },
        new WeaponSlot { name = "Snake Eyes",     description = "Hitscan pistol. Snake eyes turns enemies to stone; stone takes double damage." },
        new WeaponSlot { name = "Heads or Tails", description = "Coin launcher. Coins explode, ricochet and drop ammo or health." },
    };

    [Tooltip("Which slot you start holding.")]
    public int startingSlot = 0;

    /// <summary>Fires with the new slot index when the held weapon changes.</summary>
    public event Action<int> OnWeaponChanged;

    public int CurrentIndex { get; private set; } = -1;
    public WeaponSlot Current => CurrentIndex >= 0 ? slots[CurrentIndex] : null;

    void Start()
    {
        // Find Fortune's Edge automatically if its slot is empty
        if (slots.Length > 0 && slots[0].weaponObject == null)
        {
            FortunesEdge sword = GetComponentInChildren<FortunesEdge>(true);
            if (sword != null) slots[0].weaponObject = sword.gameObject;
        }

        int start = IsUnlocked(startingSlot) ? startingSlot : FirstUnlocked();
        if (start >= 0) Equip(start);
    }

    public bool IsUnlocked(int index) =>
        index >= 0 && index < slots.Length && slots[index].weaponObject != null;

    /// <summary>Switches to the weapon in this slot. Returns false if it's locked.</summary>
    public bool Equip(int index)
    {
        if (!IsUnlocked(index)) return false;

        // Clear the ammo counter first; ranged weapons show their own when they switch on
        if (GameHUD.Instance != null) GameHUD.Instance.SetAmmo(null);

        for (int i = 0; i < slots.Length; i++)
            if (slots[i].weaponObject != null && i != index) slots[i].weaponObject.SetActive(false);
        slots[index].weaponObject.SetActive(false);   // off/on so it always runs its equip setup
        slots[index].weaponObject.SetActive(true);

        CurrentIndex = index;
        OnWeaponChanged?.Invoke(index);
        return true;
    }

    private int FirstUnlocked()
    {
        for (int i = 0; i < slots.Length; i++) if (IsUnlocked(i)) return i;
        return -1;
    }
}
