using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TaffyMinigame : MonoBehaviour
{

    /*
     * TAFFY MINIGAME PROMPT SCRIPT:
     * The TAFFY minigame prompt is used to prompt TAFFY to teach the player about minigames
     *
     * @Author : Thomas Berner
     * Jan, 20, 2024
     */
    
    [Header("Taffy Location")]
    [Tooltip("False for Navigator, True for Vitalist")]
    [SerializeField] private bool vitalist;

    [Header("TAFFY Dialogue")] // Dialogue for bubble
    [SerializeField] private List<int> lines;

    [Header("Taffy Face Anim")]
    [SerializeField] private TaffyManager.AnimType taffyFaceChange;
    
    [Header("Taffy Tutorial")]
    [SerializeField] private bool EndTutorial;
    [SerializeField] private Animator DoorAnimator;
    [SerializeField] private string AnimTriggerName = "Open";

    [Header("Minigame")]
    [Tooltip("After which line do you want the minigame to be prompted? (use the same key as in line")]
    [SerializeField] private int MinigameLine;
    [Tooltip("Which minigame to queue up")]
    [SerializeField] private MinigameQueue.Minigames MinigameType;

    private bool hit;
    private NetAction listAddPrompt;
    private NetAction DoorAnimCall;

    void Start()
    {
        listAddPrompt = new NetAction("listAddPrompt" + gameObject.name);
        listAddPrompt += setManagerList;
        DoorAnimCall = new NetAction("TAFFY_MINI_doorAnimCall" + gameObject.name);
        DoorAnimCall += DoorAnimCallFunc;
    }

    private void setManagerList()
    {
        TaffyManager.Instance.lineIntList.Clear();
        
        for (int i = 0; i < lines.Count; i++)
        {
            int t = lines[i];
            if ((int?)t >= 0)
            {
                TaffyManager.Instance.lineIntList.Add(t);
            }
        }
    }
    private void DoorAnimCallFunc()
    {
        StartCoroutine(nameof(DoorAnimCallCoroutine));
    }

    private IEnumerator DoorAnimCallCoroutine()
    {
        while (!TaffyManager.Instance.TaffyAnimTrigger)
        {
            yield return null; 
        }
        DoorAnimator.SetTrigger(AnimTriggerName);
        if (EndTutorial)
        {
            GameManager.Instance.Vitalist.TutorialMode = false;
            GameManager.Instance.Vitalist.RestartPopupTimer();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player") && !hit)
        {
            hit = true;
            TaffyManager.Instance.PromptMinigame = (MinigameLine * 10) + (int)MinigameType;
            listAddPrompt.Invoke(() => Whoami.AmIP1());

            if (DoorAnimator != null)
                DoorAnimCall.Invoke(() => Whoami.AmIP1());

            TaffyManager.FireMinigameLines.Invoke(() => Whoami.AmIP1(), vitalist);
        }
    }
}
