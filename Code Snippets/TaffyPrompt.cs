using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TaffyPrompt : MonoBehaviour
{
    /*
     * TAFFY PROMPT SCRIPT:
     * The TAFFY prompt is used to prompt TAFFY to teach the player about inputs or nearby enviroment objects
     * 
     * @Author : Thomas Berner
     * Jan, 16, 2024
     */

    [Header("Taffy Location")]
    [Tooltip("False for Navigator, True for Vitalist")]
    [SerializeField] private bool vitalist;
    
    [Header("TAFFY Dialogue")] // Dialogue for bubble
    [SerializeField] private List<int> lines;

    [Header("Taffy Face Anim")]
    [SerializeField] private TaffyManager.AnimType taffyFaceChange;

    [Header("TAFFY Tutorial")] 
    [SerializeField] private bool FirstRoom;
    [SerializeField] private Animator DoorAnimator;
    [SerializeField] private string AnimTriggerName = "Open";
    [SerializeField] private bool EndTutorialState;
    
    private bool hit;
    private NetAction listAddPrompt;
    private NetAction doorUnlockWait;

    void Start()
    {
        listAddPrompt = new NetAction("listAddPrompt" + gameObject.name);
        listAddPrompt += setManagerList;

        doorUnlockWait = new NetAction("doorUnlockWait" + gameObject.name);
        doorUnlockWait += DoorUnlockWait;
    }

    private void OnDisable()
    {
        listAddPrompt -= setManagerList;
        doorUnlockWait -= DoorUnlockWait;
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

    private void DoorUnlockWait()
    {
        StartCoroutine(nameof(WaitForDoor));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Player") && !hit)
        {
            SoundManager.Instance.VoiceSource.clip = null;

            hit = true;
            listAddPrompt.Invoke(() => Whoami.AmIP1());

            if (!FirstRoom)
            {
                TaffyManager.FireTaffyLines.Invoke(() => Whoami.AmIP1(), vitalist);
            }
            else
            {
                TaffyManager.FireTutorialLines.Invoke(() => Whoami.AmIP1(), vitalist);
            }
            // if animator is set, anim after lines
            if (DoorAnimator != null) doorUnlockWait.Invoke(() => Whoami.AmIP1());
            if (!vitalist) TaffyManager.TaffyFaceChange.Invoke(() => Whoami.AmIP1(), (int)taffyFaceChange);
        }
    }

    private IEnumerator WaitForDoor()
    {
        while (!TaffyManager.Instance.TaffyAnimTrigger)
        {
            yield return null;
        }

        if (DoorAnimator != null)
        {
            DoorAnimator?.SetTrigger(AnimTriggerName);
        }
        if (EndTutorialState)
            GameManager.Instance.Vitalist.TutorialMode = false;
    }
}
