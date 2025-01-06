using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Linq;
using UnityEngine.UI;
using System;

public class TaffyManager : AInstance<TaffyManager>, IReloadable
{
    /*
     * TAFFY MANAGER:
     * Provides all functions for taffy
     * takes in integers to play a given voice line
     * and check for inputs in tutorial to progress players
     * 
     * @Author : Thomas Berner
     */

    // for animation changing
    public enum AnimType
    {
        Default = 0,
        Exclamation = 1,
        Question = 2,
        Determined = 3,
        Confused = 4,
        Fear = 5,
        Sad = 6,
        Tears = 7,
        Reset = 8
    }

    // TAFFY VOICE LINE FILE/ARRAY
    private static readonly string FilePath = System.IO.Path.Combine(Application.streamingAssetsPath, "TaffyVoiceLines.dat");
    private readonly string[] _taffyLines = System.IO.File.ReadAllLines(FilePath);

    // NETWORK
    public static NetAction<Boolean> FireTaffyLines = new NetAction<Boolean> ("net_fire_taffy_lines");
    public static NetAction<Boolean> FireTutorialLines = new NetAction<Boolean> ("net_fire_tutorial_lines");
    public static NetAction<Boolean> FireMinigameLines = new NetAction<Boolean> ("net_fire_minigame_lines");
    public static NetAction<Interger> TaffyFaceChange = new NetAction<Interger> ("net_taffy_face_change");
    public static NetAction DebugSkip = new NetAction ("DebugSkipTAFFY");

    private NetRoutine EndTutorialRoutine;
    private static bool lastTaffyPos;
    private static bool active = false;
    public bool TaffyActive { get { return active; } }

    private static TaffyManager instance;
    private static readonly int Emote = Animator.StringToHash("Emote");

    public List<int> lineIntList; // added to by prompt trigger
    private static Synchronized<Boolean> inputsComplete = new Synchronized<Boolean>("inputs_complete");
    private static Synchronized<Interger> promptMinigame = new Synchronized<Interger>("prompt_minigame");
    private static Synchronized<Boolean> minigameComplete = new Synchronized<Boolean>("minigame_complete");
    private static Synchronized<Boolean> taffyAnimTrigger = new Synchronized<Boolean>("anim_trigger_taffy");
    public bool InputsComplete { get { return (bool)inputsComplete.GetValue(); } }
    public int PromptMinigame { get { return (int)promptMinigame.GetValue(); } set { promptMinigame.SetValue(value, () => Whoami.AmIP1()); } }
    public bool MinigameComplete { get { return (bool)minigameComplete.GetValue(); } set { minigameComplete.SetValue(value, () => Whoami.AmIP2()); } }
    public bool TaffyAnimTrigger { get { return (bool)taffyAnimTrigger.GetValue(); } set { taffyAnimTrigger.SetValue(value, () => Whoami.AmIP1()); } }

    [Header("TAFFY IDLE")]
    [SerializeField] private Image taffyIdle;
    
    [Header("TAFFY NAVIGATOR")]
    [SerializeField] private GameObject taffyNavigator;
    [SerializeField] private TextMeshProUGUI textNavigator;
    
    [Header("TAFFY VITALIST")]
    [SerializeField] private GameObject taffyVitalist;
    [SerializeField] private TextMeshProUGUI textVitalist;

    [Header("ANIMATION POSITIONS")]
    [SerializeField] private GameObject onNavigator;
    [SerializeField] private GameObject offNavigator;
    
    [Header("TUTORIAL PROMPT")]
    [SerializeField] private NavigatorTutorialPrompts prompt;

    [Header("ANIMATORS")]
    [SerializeField] private Animator animatorNavSprites;
    [SerializeField] private Animator animatorNavPos;
    [SerializeField] private Animator wireAnimNav;
    [SerializeField] private Animator wireAnimVit;

    [Header("DEV TOOLS")]
    [Tooltip("Enable if we want to use CTRL + T to skip taffy lines")]
    [SerializeField] private bool debugMode;

    
    public void OnReload()
    {
        //
    }
    
    public void OnEnable()
    {
        inputsComplete.SetValue(false, () => { return true; });
        promptMinigame.SetValue(0, () => { return true; });
        minigameComplete.SetValue(true, () => { return true; });
        taffyAnimTrigger.SetValue(false, () => { return true; });
        ReloadingEntities.reloadableEntities += OnReload;
        FireTaffyLines += FireLinesCoroutine;
        FireTutorialLines += FireTutorialCoroutine;
        TaffyFaceChange += AnimChange;
        FireMinigameLines += FireMinigameCoroutine;
        DebugSkip += DebugSkipLines;
    }
    public void OnDisable()
    {
        ReloadingEntities.reloadableEntities -= OnReload;
        FireTaffyLines -= FireLinesCoroutine;
        FireTutorialLines -= FireTutorialCoroutine;
        TaffyFaceChange -= AnimChange;
        FireMinigameLines -= FireMinigameCoroutine;
        DebugSkip -= DebugSkipLines;
    }

    private void Start()
    {
        taffyNavigator.SetActive(false);
        taffyNavigator.transform.position = offNavigator.transform.position;
        taffyVitalist.SetActive(false);
        taffyIdle.color = Color.white;
        EndTutorialRoutine = new NetRoutine(EndTutorialPrompts, Ownership.Navigator);
        inputsComplete.SetValue(false, () => Whoami.AmIP1());
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.T) && debugMode)
        {
            DebugSkip.Invoke(() => Whoami.AmIP1());
        }
    }

    /// <summary>
    /// Just for testing purposes, skip current lines + queue up current minigame + open door if first room
    /// </summary>
    private void DebugSkipLines()
    {
        if (active)
        {
            if (!InputsComplete) // skip over first room 
            { 
                inputsComplete.SetValue(true, () => Whoami.AmIP1());
                EndTutorialPrompts(); 
            }

            if ( !(bool)minigameComplete.GetValue() && 
                Vitalist.MinigameStateMachine.GetCurrentState() != MinigameQueue.MinigameToState((MinigameQueue.Minigames)(PromptMinigame % 10)) )
            {
                GameManager.instance.Vitalist.minigameTrigger.Invoke((MinigameQueue.Minigames)(PromptMinigame % 10));
            }
            if (PromptMinigame % 10 == 6)
                GameManager.Instance.Vitalist.unlockButton.Invoke(1);
            TaffyAnimTrigger = true;
            StopLines();
            StartCoroutine(nameof(Lower), lastTaffyPos);
        }
    }

    private static void StopLines()
    {
        Instance.StopCoroutine(nameof(PlayLines));
        Instance.StopCoroutine(nameof(PlayMinigameLines));
        Instance.StopCoroutine(nameof(PlayTutorialLines));
    }

    private static void FireLinesCoroutine(Boolean location)
    {
        if (active)
        {
            StopLines();
            if ((bool)location != lastTaffyPos)
            {
                Instance.StartCoroutine(nameof(Lower), lastTaffyPos);
                SoundManager.Instance.VoiceSource.Stop();
            }
        }

        Instance.StartCoroutine(nameof(FireLines), location);
    }

    private static void FireTutorialCoroutine(Boolean location)
    {
        if (active)
        {
            StopLines();
            if ((bool)location != lastTaffyPos)
            {
                Instance.StartCoroutine(nameof(Lower), lastTaffyPos);
                SoundManager.Instance.VoiceSource.Stop();
            }
        }

        Instance.StartCoroutine(nameof(FireTutorial), location);
    }

    private static void FireMinigameCoroutine(Boolean location)
    {
        if (active)
        {
            StopLines();
            if ((bool)location != lastTaffyPos)
            {
                Instance.StartCoroutine(nameof(Lower), lastTaffyPos);
                SoundManager.Instance.VoiceSource.Stop();
            }
        }

        Instance.StartCoroutine(nameof(FireMinigame), location);
    }

    /// <summary>
    /// Called when a taffy is triggered to raise, speak, and lower back down.
    /// when location is true, taffy moves to navigator, otherwise go to vitalist
    /// </summary>
    /// <returns></returns>
    private IEnumerator FireLines(Boolean location)
    {   
        lastTaffyPos = (bool)location;
        
        yield return Instance.StartCoroutine(nameof(Raise), (bool)location);
        yield return Instance.StartCoroutine(nameof(PlayLines), (bool)location);
        yield return Instance.StartCoroutine(nameof(Lower), (bool)location);
    }

    /// <summary>
    /// Called when taffy is triggered in the first room for the tutorial version of the prompt
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    private IEnumerator FireTutorial(Boolean location)
    {
        lastTaffyPos = (bool)location;
        
        yield return Instance.StartCoroutine(nameof(Raise), (bool)location);
        prompt.gameObject.SetActive(true);
        yield return Instance.StartCoroutine(nameof(PlayTutorialLines), (bool)location);
        yield return Instance.StartCoroutine(nameof(Lower), (bool)location);
    }

    /// <summary>
    /// Called when taffy is triggered with the minigame prompt
    /// </summary>
    /// <param name="location"></param>
    /// <returns></returns>
    private IEnumerator FireMinigame(Boolean location)
    {

        if (PromptMinigame%10 == (int)MinigameQueue.Minigames.defib) //Defib exclusive case
        {
            active = true;
            minigameComplete.SetValue(false, () => { return true; });

            StatsManager.DefibTriggered.Invoke(() => Whoami.AmIP1());
            
            yield return new WaitUntil(() => (bool)minigameComplete.GetValue());
        }
        
        lastTaffyPos = (bool)location;
        
        yield return Instance.StartCoroutine(nameof(Raise), (bool)location);
        yield return Instance.StartCoroutine(nameof(PlayMinigameLines), (bool)location);
        yield return Instance.StartCoroutine(nameof(Lower), (bool)location);
    }

    /// <summary>
    /// Raise taffy on screen ready for lines to be displayed
    /// </summary>
    private IEnumerator Raise(bool location)
    {
        taffyIdle.color = Color.black;
        active = true;
        if (!location) //nav
        {

            taffyNavigator.SetActive(true);
            wireAnimNav.SetBool("Raise", true);
            yield return new WaitForSeconds((wireAnimNav.GetCurrentAnimatorStateInfo(0).length / 4) * 3);
            animatorNavPos.SetBool("Raise", true);
            yield return new WaitForSeconds(animatorNavPos.GetCurrentAnimatorStateInfo(0).length);
        }
        else //vit
        {
            wireAnimNav.SetBool("Raise", true);
            yield return new WaitForSeconds((wireAnimNav.GetCurrentAnimatorStateInfo(0).length / 4) * 3);
            taffyVitalist.SetActive(true);
        }
    }

    /// <summary>
    /// lower taffy of screen and deactivate
    /// </summary>
    private IEnumerator Lower(bool location)
    {
        if (!location) //nav
        {
            animatorNavPos.SetBool("Raise", false);
            yield return new WaitForSeconds(animatorNavPos.GetCurrentAnimatorStateInfo(0).length);
            wireAnimNav.SetBool("Raise", false);
            yield return new WaitForSeconds(wireAnimNav.GetCurrentAnimatorStateInfo(0).length);

            taffyNavigator.transform.position = offNavigator.transform.position;
            textNavigator.text = "";
        }
        else //vit
        {
            taffyVitalist.SetActive(false);
            wireAnimVit.SetBool("Raise", false);
            yield return new WaitForSeconds(wireAnimNav.GetCurrentAnimatorStateInfo(0).length);
            textVitalist.text = "";
        }

        AnimChange(8);
        TaffyAnimTrigger = false;
        taffyIdle.color = Color.white;
        active = false;
    }

    /// <summary>
    /// Play all voice lines given and set text on proper screen
    /// </summary>
    /// <returns></returns>
    private IEnumerator PlayLines(bool location)
    {
        int i = 0;
        if (!location) //nav
        {
            for (int j = 0; j < lineIntList.Count; j++)
            {
                i = lineIntList[j];
                textNavigator.text = _taffyLines[i];
                SoundManager.Instance.runVoiceLine(i.ToString());
                yield return new WaitForSeconds(SoundManager.Instance.VoiceSource.clip.length);
            }
        }
        else //vit
        {
            for (int j = 0; j < lineIntList.Count; j++)
            {
                i = lineIntList[j];
                textVitalist.text = _taffyLines[i];
                SoundManager.Instance.runVoiceLine(i.ToString());
                yield return new WaitForSeconds(SoundManager.Instance.VoiceSource.clip.length);
            }
        }

        yield return new WaitForEndOfFrame();
        TaffyAnimTrigger = true;
    }

    /// <summary>
    /// a play lines function that waits for the check inputs instead
    /// </summary>
    /// <returns></returns>
    private IEnumerator PlayTutorialLines()
    {
        if (Whoami.AmIP1()) StartCoroutine(nameof(CheckForInputs));
        int i = 0;
        for (int j = 0; j < lineIntList.Count; j++)
        {
            i = lineIntList[j];
            textNavigator.text = _taffyLines[i];
            SoundManager.Instance.runVoiceLine(i.ToString());
            yield return new WaitForSeconds(SoundManager.Instance.VoiceSource.clip.length);

            yield return new WaitUntil(() => (bool)inputsComplete.GetValue());
        }
        
        yield return new WaitForEndOfFrame();
        TaffyAnimTrigger = true;
    }

    private IEnumerator PlayMinigameLines(bool location)
    {
        int i = 0;
        if (!location) //navigator 
        {
            switch (PromptMinigame % 10)
            {
                case 8: // Defib
                    for (int j = 0; j < lineIntList.Count; j++)
                    {
                        i = lineIntList[j];
                        textNavigator.text = _taffyLines[i];
                        SoundManager.Instance.runVoiceLine(i.ToString());
                        yield return new WaitForSeconds(SoundManager.Instance.VoiceSource.clip.length);
                    }

                    break;
                default:
                    for (int j = 0; j < lineIntList.Count; j++)
                    {
                        i = lineIntList[j];
                        textNavigator.text = _taffyLines[i];
                        SoundManager.Instance.runVoiceLine(i.ToString());
                        yield return new WaitForSeconds(SoundManager.Instance.VoiceSource.clip.length);

                        if (i == (PromptMinigame / 10)) // check for element matching line
                        {
                            if (Vitalist.MinigameStateMachine.GetCurrentState() != MinigameQueue.MinigameToState((MinigameQueue.Minigames)(PromptMinigame % 10)))
                                GameManager.instance.Vitalist.minigameTrigger.Invoke((MinigameQueue.Minigames)(PromptMinigame % 10));
                            if (PromptMinigame % 10 == 6)
                                GameManager.Instance.Vitalist.unlockButton.Invoke(1);
                            minigameComplete.SetValue(false, () => { return true; });
                            yield return new WaitUntil(() => (bool)minigameComplete.GetValue());
                        }
                    }

                    break;
            }
        }
        else 
        {
            for (int j = 0; j < lineIntList.Count; j++)
            {
                i = lineIntList[j];
                textVitalist.text = _taffyLines[i];
                SoundManager.Instance.runVoiceLine(i.ToString());
                yield return new WaitForSeconds(SoundManager.Instance.VoiceSource.clip.length);
                
                if (i == (PromptMinigame / 10))
                {
                    if (Vitalist.MinigameStateMachine.GetCurrentState() != MinigameQueue.MinigameToState((MinigameQueue.Minigames)(PromptMinigame % 10)))
                        GameManager.instance.Vitalist.minigameTrigger.Invoke((MinigameQueue.Minigames)(PromptMinigame % 10));
                    if (PromptMinigame % 10 == 6)
                        GameManager.Instance.Vitalist.unlockButton.Invoke(1);
                    minigameComplete.SetValue(false, () => { return true; });
                    yield return new WaitUntil(() => (bool)minigameComplete.GetValue());
                }
            }
        }
        yield return new WaitForEndOfFrame();
        TaffyAnimTrigger = true;
    }

    /// <summary>
    /// Check for inputs from the navigator in the first room to progress
    /// </summary>
    /// <returns></returns>
    private IEnumerator CheckForInputs()
    {
        if (GameManager.Instance.Navigator.Input != null)
        {
            //attach bool vars to events on controller input
            
            bool taskWalk = false;
            bool taskLook = false;

            bool[] inputs = new bool[5];
            while (!(bool)inputsComplete.GetValue())
            {
                inputs[0] = inputs[0] || GameManager.Instance.Navigator.Input.AnalogueAxis.x < -0.3f;
                inputs[1] = inputs[1] || GameManager.Instance.Navigator.Input.AnalogueAxis.x > 0.3f;
                inputs[2] = inputs[2] || GameManager.Instance.Navigator.Input.AnalogueAxis.y < -0.3f;
                inputs[3] = inputs[3] || GameManager.Instance.Navigator.Input.AnalogueAxis.y > 0.3f;
                inputs[4] = inputs[4] || GameManager.Instance.Navigator.Input.AnalogueCAxis.magnitude > 1.0f;
                
                // If we have walked in all directions.
                // This linq line is kinda cursed, so let me take you through it.
                // We only care about the first four elements so in both cases we take those.
                // afterwards, we take the first two if we want to compare the horizontal axis and the last two if we want to check the vertical axis
                // Then we check if we have pressed any of those buttons.

                if (inputs.Take(4).Take(2).Any(x => x == true) && inputs.Take(4).TakeLast(2).Any(x => x == true))
                {
                    prompt.CompleteWalkPrompt();
                    taskWalk = true;
                }
                //If we have looked around.
                if (inputs.TakeLast(1).All(x => x == true))
                {
                    prompt.CompleteLookPrompt();
                    taskLook = true;
                }
                // On completing all.
                if (taskLook && taskWalk)    
                {
                    inputsComplete.SetValue(true, () => Whoami.AmIP1());
                    do
                    {
                        EndTutorialRoutine.Invoke();
                        yield return new WaitForSecondsRealtime(0.5f);
                    } while (!GameManager.Instance.Vitalist.gameObject.activeInHierarchy);
                    
                }
                yield return new WaitForEndOfFrame();
            }
        }
    }
    private void EndTutorialPrompts()
    {
        prompt.StopTutorialPrompts();
    }

    // Changes taffy animation parameters when animType change
    private void AnimChange(Interger input)
    {
        animatorNavSprites.SetInteger("Emote", (int)input);
    }
}
