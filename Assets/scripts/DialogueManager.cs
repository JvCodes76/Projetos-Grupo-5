using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; 
using UnityEngine.SceneManagement; 
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public TextMeshProUGUI dialogueText; // A caixa de texto 
    public Image dialoguePanelImage;
    public float typingSpeed = 0.05f;    
    public Color activeColor = Color.green; 
    public Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Cor para o fundo

    [System.Serializable]
    public class DialogueLine
    {
        public string speaker; 
        [TextArea(3, 10)]
        public string line; 
    }

    public DialogueLine[] dialogue;
    private int currentLineIndex = 0;
    private bool isTyping = false;

    public string nextSceneName = "PrimeiraFase"; 


    void Start()
    {
        dialoguePanelImage.color = inactiveColor;
        StartDialogue();
    }

    void Update()
    {
        if (Input.GetButtonDown("Fire1")) 
        {
            if (isTyping)
            {
                StopAllCoroutines();
                dialogueText.text = dialogue[currentLineIndex - 1].line;
                isTyping = false;
            }
            else
            {
                DisplayNextLine();
            }
        }
    }

    public void StartDialogue()
    {
        currentLineIndex = 0;
        if (dialogue.Length > 0)
        {
            DisplayNextLine();
        }
    }

    void DisplayNextLine()
    {
        if (currentLineIndex >= dialogue.Length)
        {
            EndDialogue();
            return;
        }

        SetPanelActive(dialogue[currentLineIndex].speaker);

        StartCoroutine(TypeSentence(dialogue[currentLineIndex].line));

        currentLineIndex++;
    }
    
    IEnumerator TypeSentence(string sentence)
    {
        isTyping = true;
        dialogueText.text = ""; 
        
        foreach (char letter in sentence.ToCharArray())
        {
            dialogueText.text += letter;
            yield return new WaitForSeconds(typingSpeed); 
        }
        
        isTyping = false;
    }

    // Lógica para ligar/desligar a cor do painel
    void SetPanelActive(string speaker)
    {
        if (speaker == "FALA") // Se tiver um diálogo
        {
            dialoguePanelImage.color = activeColor;
        }
        else // Se for uma pausa ou outro estado
        {
            dialoguePanelImage.color = inactiveColor;
        }
    }

    void EndDialogue()
    {
        Debug.Log("Diálogo Terminado. Carregando Fase.");
        SceneManager.LoadScene(nextSceneName);
    }
}