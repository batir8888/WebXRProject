using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Visual response to XR Interaction Toolkit events:
/// hover - lighter color and a white outline;
/// select (grab) - orange color, pulsing yellow outline and a short "pop" when grabbed.
/// Also pauses the idle spin while the object is held.
/// </summary>
[RequireComponent(typeof(XRBaseInteractable))]
public class InteractionFeedback : MonoBehaviour
{
  private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

  [SerializeField] private Renderer bodyRenderer;
  [Tooltip("Inverted-hull outline renderer (child mesh with front faces culled).")]
  [SerializeField] private Renderer outlineRenderer;
  [Tooltip("Behaviour disabled while the object is selected (e.g. Spin).")]
  [SerializeField] private Behaviour pauseWhileSelected;

  [SerializeField] private Color idleColor = new Color(0.1f, 0.65f, 1f);
  [SerializeField] private Color hoverColor = new Color(0.45f, 0.9f, 1f);
  [SerializeField] private Color selectColor = new Color(1f, 0.55f, 0.1f);
  [SerializeField] private Color hoverOutlineColor = Color.white;
  [SerializeField] private Color selectOutlineColor = new Color(1f, 0.9f, 0.2f);
  [SerializeField] private float outlineThickness = 0.06f;
  [SerializeField] private float colorLerpSpeed = 14f;

  private XRBaseInteractable interactable;
  private MaterialPropertyBlock block;
  private Color currentColor;
  private float pop;

  private void Awake()
  {
    interactable = GetComponent<XRBaseInteractable>();
    block = new MaterialPropertyBlock();
    currentColor = idleColor;
    if (outlineRenderer != null) outlineRenderer.enabled = false;
  }

  private void OnEnable()
  {
    interactable.hoverEntered.AddListener(OnHoverEntered);
    interactable.selectEntered.AddListener(OnSelectEntered);
    interactable.selectExited.AddListener(OnSelectExited);
  }

  private void OnDisable()
  {
    interactable.hoverEntered.RemoveListener(OnHoverEntered);
    interactable.selectEntered.RemoveListener(OnSelectEntered);
    interactable.selectExited.RemoveListener(OnSelectExited);
  }

  private void OnHoverEntered(HoverEnterEventArgs args) => pop = Mathf.Max(pop, 0.5f);

  private void OnSelectEntered(SelectEnterEventArgs args)
  {
    pop = 1f;
    if (pauseWhileSelected != null) pauseWhileSelected.enabled = false;
    Debug.Log($"[InteractionFeedback] {name} grabbed by {args.interactorObject.transform.name}");
  }

  private void OnSelectExited(SelectExitEventArgs args)
  {
    if (!interactable.isSelected && pauseWhileSelected != null) pauseWhileSelected.enabled = true;
    Debug.Log($"[InteractionFeedback] {name} released, scale {transform.localScale.x:F3}");
  }

  private void Update()
  {
    bool selected = interactable.isSelected;
    bool hovered = interactable.isHovered;

    Color target = selected ? selectColor : hovered ? hoverColor : idleColor;
    currentColor = Color.Lerp(currentColor, target, 1f - Mathf.Exp(-colorLerpSpeed * Time.deltaTime));
    pop = Mathf.MoveTowards(pop, 0f, Time.deltaTime * 4f);

    if (bodyRenderer != null)
    {
      bodyRenderer.GetPropertyBlock(block);
      block.SetColor(BaseColorId, Color.Lerp(currentColor, Color.white, pop * 0.35f));
      bodyRenderer.SetPropertyBlock(block);
    }

    if (outlineRenderer != null)
    {
      bool show = selected || hovered;
      outlineRenderer.enabled = show;
      if (show)
      {
        float pulse = selected ? 0.5f + 0.5f * Mathf.Sin(Time.time * 10f) : 0f;
        float thickness = outlineThickness * (1f + 0.6f * pulse) + 0.08f * pop;
        outlineRenderer.transform.localScale = Vector3.one * (1f + thickness);
        outlineRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, selected ? selectOutlineColor : hoverOutlineColor);
        outlineRenderer.SetPropertyBlock(block);
      }
    }
  }
}
