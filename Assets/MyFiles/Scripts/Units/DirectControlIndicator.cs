using System;
using UnityEngine;
using TMPro;

/// <summary>
/// Visual indicator that appears above units under direct control
/// </summary>
public class DirectControlIndicator : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] private TextMeshPro controlText;
    [SerializeField] private MeshRenderer iconRenderer;
    [SerializeField] private Color controlTextColor = Color.cyan;
    [SerializeField] private float pulseSpeed = 1.5f;
    [SerializeField] private float pulseRange = 0.2f;
    [SerializeField] private float rotationSpeed = 30f;
    [SerializeField] private string controlMessage = "PLAYER CONTROL";

    [Header("Behavior")]
    [SerializeField] private bool alwaysFaceCamera = true;
    [SerializeField] private float floatHeight = 2.5f;
    [SerializeField] private float bobAmount = 0.2f;
    [SerializeField] private float bobSpeed = 1f;

    // Private vars
    private Camera mainCamera;
    private float initialScale;
    private float timeOffset;

    private void Awake()
    {
        try
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                Debug.LogWarning("Main camera not found for DirectControlIndicator");
            }

            // Setup text if available
            if (controlText != null)
            {
                controlText.text = controlMessage;
                controlText.color = controlTextColor;
            }

            // Initialize variables
            initialScale = transform.localScale.y;
            timeOffset = UnityEngine.Random.Range(0f, 2f * Mathf.PI); // Random start point for animation

            // Parent may not be set yet, so position in Update
            transform.localPosition = Vector3.up * floatHeight;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in DirectControlIndicator.Awake: {e.Message}");
        }
    }

    private void Update()
    {
        try
        {
            // Face camera
            if (alwaysFaceCamera && mainCamera != null)
            {
                transform.rotation = Quaternion.LookRotation(
                    transform.position - mainCamera.transform.position);
            }

            // Animate icon
            AnimateIndicator();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in DirectControlIndicator.Update: {e.Message}");
        }
    }

    private void AnimateIndicator()
    {
        // Pulsing scale effect
        float pulseValue = 1f + pulseRange * Mathf.Sin((Time.time + timeOffset) * pulseSpeed);
        transform.localScale = new Vector3(pulseValue, pulseValue, pulseValue) * initialScale;

        // Bobbing height effect
        float bobValue = Mathf.Sin((Time.time + timeOffset) * bobSpeed) * bobAmount;
        transform.localPosition = Vector3.up * (floatHeight + bobValue);

        // Rotate the icon if we have one
        if (iconRenderer != null)
        {
            iconRenderer.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }
}