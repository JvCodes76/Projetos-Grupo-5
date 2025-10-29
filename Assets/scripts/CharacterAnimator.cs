using UnityEngine;
public class CharacterAnimator : MonoBehaviour
{
    private characterMovement characterMovement;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    void Start()
    {
        characterMovement = GetComponent<characterMovement>();
        if (characterMovement == null)
        {
            Debug.LogError("faltando o CharacterMovement no player");
            enabled = false;
            return;
        }

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("faltando animator no seu player");
            enabled = false;
            return;
        }

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("ERRO: Faltando SpriteRenderer no player para virar o personagem!");
        }
    }

    void Update()
    {
        if (animator == null || characterMovement == null) return;
        //pra pegar as velocidades
        float currentSpeed = Mathf.Abs(characterMovement.currentHorizontalVelocity);

        float verticalVelocity = characterMovement.rb.linearVelocity.y;

        animator.SetFloat("Speed", currentSpeed);

        //ver se ta tocando o chao
        animator.SetBool("IsGrounded", characterMovement.onGround);

        //saber se ta subindo ou caindo 
        animator.SetFloat("VerticalSpeed", verticalVelocity);

        //ver se ta deslizando
        animator.SetBool("IsWallSliding", characterMovement.isWallSliding);

        //wall jump
        animator.SetBool("IsWallJumping", characterMovement.isWallJumping);

        FlipCharacter(characterMovement.horizontalInput);
    }
    private void FlipCharacter(float inputX)
    {
        if (spriteRenderer == null || inputX == 0) return;

        if (inputX > 0)
        {
            spriteRenderer.flipX = false;
        }
        else if (inputX < 0)
        {
            spriteRenderer.flipX = true;
        }
    }
}