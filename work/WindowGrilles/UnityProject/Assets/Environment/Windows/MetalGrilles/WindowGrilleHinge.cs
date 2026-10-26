using UnityEngine;

/// <summary>Controls the two leaves of an imported hinged window grille.</summary>
[DisallowMultipleComponent]
public sealed class WindowGrilleHinge : MonoBehaviour
{
    [SerializeField] private Animator animator;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    [ContextMenu("Открыть обе створки")]
    public void Open() => SetOpen(true);
    [ContextMenu("Закрыть обе створки")]
    public void Close() => SetOpen(false);
    public void Toggle() => SetOpen(!IsOpen);

    public void SetOpen(bool open)
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        IsOpen = open;
        if (animator != null)
            animator.SetBool("Open", open);
    }
}
