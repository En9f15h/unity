using UnityEngine;

public class CharacterSelectionAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private CanvasGroup canvasGroup;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }

    public void PlayTrigger(string triggerName)
    {
        if (triggerName == "VSFlash")
            foreach (var image in GetComponentsInChildren<UnityEngine.UI.Image>(true))
                UIShaderFeedback.Ensure(image).Pulse(0.7f);
        if (animator == null || string.IsNullOrEmpty(triggerName) || !HasTrigger(triggerName))
            return;

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
    }

    public void SetVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
            return;
        }

        gameObject.SetActive(visible);
    }

    private bool HasTrigger(string triggerName)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].name == triggerName)
                return true;
        }

        return false;
    }
}
