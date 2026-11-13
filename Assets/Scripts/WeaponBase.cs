using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Shared parent for every weapon (Fortune's Edge, The Jackpot, Snake Eyes, Heads or Tails).
/// Handles attack input, equip/unequip and access to the LuckSystem.
/// Only one weapon is held at a time: the active one is the one whose GameObject is enabled.
/// </summary>
public abstract class WeaponBase : MonoBehaviour
{
    [Header("Weapon")]
    public string weaponName = "Weapon";

    [Tooltip("1 = normal. Nike's speed boost / Ares' Bloodlust raise this to make attacks faster.")]
    public float attackSpeedMultiplier = 1f;

    /// <summary>The player that holds this weapon (root of the hierarchy).</summary>
    protected GameObject Owner { get; private set; }
    protected PlayerMovement Movement { get; private set; }
    protected Health OwnerHealth { get; private set; }

    protected virtual void Awake()
    {
        Owner = transform.root.gameObject;
        Movement = Owner.GetComponent<PlayerMovement>();
        OwnerHealth = Owner.GetComponent<Health>();
    }

    protected virtual void OnEnable() => OnEquip();
    protected virtual void OnDisable() => OnUnequip();

    protected virtual void Update()
    {
        // No attacking while paused (altar menu, game over) or dead.
        if (Time.timeScale <= 0f) return;
        if (OwnerHealth != null && OwnerHealth.IsDead) return;
        if (Cursor.lockState != CursorLockMode.Locked || Time.frameCount == PlayerMovement.LastRecaptureFrame) return;

        HandleAttackInput();
    }

    /// <summary>Each weapon decides what to do with input here.</summary>
    protected abstract void HandleAttackInput();

    /// <summary>Called when this weapon becomes the active one.</summary>
    protected virtual void OnEquip() { }

    /// <summary>Called when the player switches away. Stop swings, turn off hitboxes, etc.</summary>
    protected virtual void OnUnequip() { }

    // ---------- Input helpers (mouse left click / gamepad right trigger) ----------

    protected bool AttackPressedThisFrame()
    {
        bool mouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool pad = Gamepad.current != null && Gamepad.current.rightTrigger.wasPressedThisFrame;
        return mouse || pad;
    }

    protected bool AttackHeld()
    {
        bool mouse = Mouse.current != null && Mouse.current.leftButton.isPressed;
        bool pad = Gamepad.current != null && Gamepad.current.rightTrigger.isPressed;
        return mouse || pad;
    }

    // ---------- Luck helper ----------

    /// <summary>Rolls the dice. Returns a plain 1x Normal roll if there's no LuckSystem in the scene.</summary>
    protected RollResult RollLuck()
    {
        if (LuckSystem.Instance != null) return LuckSystem.Instance.Roll();
        return new RollResult { tier = RollTier.Normal, multiplier = 1f, dice = new int[0] };
    }
}
