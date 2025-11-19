using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BirdMovement : MonoBehaviour
{
    [Header("References")]

    public float safeDistance = 0.3f;

    [Header("Launch Settings")]
    [Tooltip("Maximum distance the bird can be pulled back from the start position.")]
    public float maxDragDistance = 3f;
    [Tooltip("Multiplier applied to the release direction to determine launch speed.")]
    public float launchPower = 5f;

    [Header("Visuals")]
    [Tooltip("Prefab used to show the launch direction and power.")]
    public GameObject dragArrowPrefab;
    [Tooltip("Multiplier applied to the arrow length relative to the drag distance.")]
    public float arrowScale = 0.3f;
    [Tooltip("Sprite to display when the bird reaches the goal (success).")]
    public Sprite successSprite;
    [Tooltip("Animation duration for the success sprite scale animation.")]
    public float successSpriteAnimationDuration = 0.5f;
    [Tooltip("Final scale of the success sprite after animation.")]
    public float successSpriteFinalScale = 1.5f;

    [Header("Physics")]
    [Tooltip("Linear drag (air resistance) applied to the bird. Higher values = more friction.")]
    public float linearDrag = 0.5f;
    [Tooltip("Angular drag (rotational resistance) applied to the bird. Higher values = less spinning.")]
    public float angularDrag = 0.5f;
    [Tooltip("Physics Material 2D for friction when colliding with surfaces. If null, will use default friction.")]
    public PhysicsMaterial2D frictionMaterial;

    Vector2 _startPosition, _initialPosition;
    Rigidbody2D _birdRigidbody;
    bool _isDragging;
    Vector2 _currentDragPosition;
    bool _hasLaunched;
    bool _isOnFloor;
    bool _isFailed;
    bool _isSuccess; // 성공 상태 플래그
    float _stopVelocityThreshold = 0.2f; // 속도가 이 값 이하로 떨어지면 멈춤
    
    GameObject _dragArrowInstance;
    GameObject _successSpriteInstance;
    Coroutine _successSpriteCoroutine;

    void Awake()
    {
        _birdRigidbody = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        _startPosition = transform.position;
        _initialPosition = transform.position;
        _currentDragPosition = _startPosition;
        SetBirdPhysics(isKinematic: true, resetVelocity: true); // 새 공중에 고정

        // Arrow prefab 인스턴스 생성
        if (dragArrowPrefab != null)
        {
            _dragArrowInstance = Instantiate(dragArrowPrefab);
            _dragArrowInstance.SetActive(false);
        }
    }

    void Update()
    {
        HandleInput();
        UpdateArrow();

        // floor에 닿아서 굴러가는 중이면 속도 체크
        if (_isOnFloor && _hasLaunched && !_birdRigidbody.isKinematic)
        {
            if (_birdRigidbody.velocity.magnitude < _stopVelocityThreshold)
            {
                // 속도가 충분히 느려지면 멈춤
                StopBirdOnFloor();
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        GameObject other = collision.gameObject;

        // 자식 오브젝트와의 충돌은 무시 (애니메이션 프리팹)
        if (other.transform.IsChildOf(transform))
        {
            return;
        }

        // goal와 충돌 - 성공 (goal 자체 또는 goal의 자식과 충돌)
        if (IsFinish(other))
        {
            Debug.Log($"BirdMovement: Hit goal ({other.name}) - SUCCESS!");
            HandleSuccess();
            return;
        }

        // Obstacle 태그를 가진 오브젝트 또는 모든 부모 계층에서 확인 - 실패 처리
        if (IsObstacle(other))
        {
            Debug.Log($"BirdMovement: Hit obstacle (by tag) - {other.name} - FAILURE!");
            StopBirdImmediately();
            HandleFailure();
            return;
        }

        // Floor 태그를 가진 오브젝트 확인- 굴러가기 시작
        if (IsFloor(other))
        {
            Debug.Log("BirdMovement: Hit floor - Bird will roll");
            _isOnFloor = true;
            // 물리 시뮬레이션은 계속 유지 (굴러가도록)
            return;
        }

        // obstacle, goal, floor가 아닌 다른 물체와 충돌 - 무시 (계속 날아감)
        Debug.Log($"BirdMovement: Hit other object ({other.name}) - Ignoring");
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // 장애물에 부딪혀서 멈춘 상태 또는 성공 상태에서 터치하면 리스타트
            if (_isFailed || _isSuccess)
            {
                ResetBirdState();
                return;
            }

            // 멈춘 상태가 아니고 아직 발사하지 않았으면 드래그 시작
            if (!_hasLaunched && IsPointerOverBird())
            {
                _isDragging = true;
            }
        }
        else if (Input.GetMouseButton(0) && _isDragging) // 드래그 중
        {
            // 드래그 중일 때 위치 업데이트
            UpdateDragVector();
        }
        else if (Input.GetMouseButtonUp(0) && _isDragging) // 드래그 후 마우스 뗌
        {
            ReleaseBird();
        }
    }

    // 마우스가 새 쪽인지 확인
    bool IsPointerOverBird()
    {
        Vector3 pointerWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        pointerWorldPos.z = 0f;
        Collider2D hit = Physics2D.OverlapPoint(pointerWorldPos);
        
        if (hit == null) return false;
        
        // Bird 자체를 클릭했거나, Bird의 자식을 클릭한 경우 true
        return hit.gameObject == gameObject || hit.transform.IsChildOf(transform);
    }

    void UpdateDragVector()
    {
        Vector3 pointerWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 pointerWorldPosXY = pointerWorldPos;

        Vector2 directionFromStart = pointerWorldPosXY - _startPosition;
        // 최대 드래그 거리 초과 방지
        if (directionFromStart.magnitude > maxDragDistance)
        {
            directionFromStart = directionFromStart.normalized * maxDragDistance;
        }

        _currentDragPosition = _startPosition + directionFromStart;
    }

    void ReleaseBird()
    {
        _isDragging = false;
        _hasLaunched = true;
        SetBirdPhysics(isKinematic: false, resetVelocity: true);

        Vector2 releaseVector = _startPosition - _currentDragPosition;
        _birdRigidbody.velocity = releaseVector * launchPower;

        _currentDragPosition = _startPosition;
        HideArrow();
    }

    void SetBirdPhysics(bool isKinematic, bool resetVelocity)
    {
        _birdRigidbody.isKinematic = isKinematic;
        // kinematic이 아닐 때만 중력 적용
        if (!isKinematic)
        {
            _birdRigidbody.gravityScale = 1f;
            // 마찰력 적용 (발사된 상태일 때만)
            _birdRigidbody.drag = linearDrag;
            _birdRigidbody.angularDrag = angularDrag;
        }
        else // 게임 시작 시 공중에 멈춰있음
        {
            _birdRigidbody.gravityScale = 0f; // kinematic일 때는 중력 없음
            _birdRigidbody.drag = 0f; // kinematic일 때는 마찰력 없음
            _birdRigidbody.angularDrag = 0f;
        }
        if (resetVelocity)
        {
            _birdRigidbody.velocity = Vector2.zero;
            _birdRigidbody.angularVelocity = 0f;
        }
    }

    void UpdateArrow()
    {
        if (_isDragging) // 드래그 중
        {
            UpdateDragVector();
        }

        if (_isDragging)
        {
            Vector2 arrowVector = _startPosition - _currentDragPosition;
            if (arrowVector.sqrMagnitude > Mathf.Epsilon) // 0 초과
            {
                _dragArrowInstance.SetActive(true);
                
                // 화살표 크기를 드래그 거리에 비례하여 조정 (Y축(화살표 방향)으로 늘어남)
                float distance = arrowVector.magnitude * arrowScale;
                _dragArrowInstance.transform.localScale = new Vector3(1f, distance, 1f);
                
                // 화살표가 발사 방향을 가리키도록 회전 (위쪽이 기본 방향이므로 -90도 보정)
                float angle = Mathf.Atan2(arrowVector.y, arrowVector.x) * Mathf.Rad2Deg - 90f;
                _dragArrowInstance.transform.rotation = Quaternion.Euler(0, 0, angle);
                
                // 화살표 바닥이 새 위치에 오도록 설정 (화살표 중심을 위로 이동)
                Vector2 arrowOffset = arrowVector.normalized * (distance * 0.5f);
                _dragArrowInstance.transform.position = new Vector3(
                    _startPosition.x + arrowOffset.x,
                    _startPosition.y + arrowOffset.y,
                    0f
                );
            }
            else
            {
                _dragArrowInstance.SetActive(false);
            }
        }
        else
        {
            HideArrow();
        }
    }

    void HideArrow()
    {
        _dragArrowInstance.SetActive(false);
    }

    // Finish 태그를 가진 오브젝트인지 확인
    bool IsFinish(GameObject obj)
    {
        if(obj.CompareTag("Finish")) return true;
        else return false;
    }

    // Obstacle 태그를 가진 오브젝트인지 확인
    bool IsObstacle(GameObject obj)
    {
        if(obj.CompareTag("Obstacle")) return true;
        else return false;
    }

    // Floor 태그를 가진 오브젝트인지 확인
    bool IsFloor(GameObject obj)
    {
        if(obj.CompareTag("Floor")) return true;
        else return false;
    }

    void HandleSuccess()
    {
        Debug.Log("Bird reached the goal: Success!");
        
        // 새를 멈춤
        if (_birdRigidbody != null)
        {
            _birdRigidbody.velocity = Vector2.zero;
            _birdRigidbody.angularVelocity = 0f;
        }

        _isDragging = false;
        _hasLaunched = false;
        _isOnFloor = false;
        _isFailed = false;
        _isSuccess = true; // 성공 상태로 설정

        // 새를 kinematic으로 변경하여 멈춤
        SetBirdPhysics(isKinematic: true, resetVelocity: true);

        HideArrow();
        
        // 성공 스프라이트 표시
        ShowSuccessSprite();

        Debug.Log("Success! Click to restart.");
    }
    
    void ShowSuccessSprite()
    {
        if (successSprite == null) return;
        
        // 기존 성공 스프라이트가 있으면 제거
        if (_successSpriteInstance != null)
        {
            Destroy(_successSpriteInstance);
        }
        
        // 성공 스프라이트를 표시할 GameObject 생성
        _successSpriteInstance = new GameObject("SuccessSprite");
        _successSpriteInstance.transform.SetParent(transform);
        _successSpriteInstance.transform.localPosition = Vector3.zero;
        
        // SpriteRenderer 추가
        SpriteRenderer spriteRenderer = _successSpriteInstance.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = successSprite;
        spriteRenderer.sortingOrder = 100; // 다른 오브젝트 위에 표시
        
        // 초기 스케일을 0으로 설정 (작은 상태에서 시작)
        _successSpriteInstance.transform.localScale = Vector3.zero;
        
        // 작았다가 커지는 애니메이션 코루틴 시작
        if (_successSpriteCoroutine != null)
        {
            StopCoroutine(_successSpriteCoroutine);
        }
        _successSpriteCoroutine = StartCoroutine(AnimateSuccessSprite());
    }
    
    IEnumerator AnimateSuccessSprite()
    {
        if (_successSpriteInstance == null) yield break;
        
        float elapsedTime = 0f;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * successSpriteFinalScale;
        
        // 작았다가 커지는 애니메이션
        while (elapsedTime < successSpriteAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / successSpriteAnimationDuration;
            
            // Ease-out 효과 (부드러운 애니메이션)
            t = 1f - Mathf.Pow(1f - t, 3f); // Cubic ease-out
            
            if (_successSpriteInstance != null)
            {
                _successSpriteInstance.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            }
            
            yield return null;
        }
        
        // 최종 스케일로 설정
        if (_successSpriteInstance != null)
        {
            _successSpriteInstance.transform.localScale = endScale;
        }
        
        _successSpriteCoroutine = null;
    }

    void HandleFailure()
    {
        Debug.Log("Bird hit an obstacle or fell: Restarting.");
        ResetBirdState();
    }

    void StopBirdImmediately()
    {
        // 새를 즉시 멈춤
        if (_birdRigidbody != null)
        {
            _birdRigidbody.velocity = Vector2.zero;
            _birdRigidbody.angularVelocity = 0f;
        }

        _isDragging = false;
        _hasLaunched = false;
        _isOnFloor = false;
        _isFailed = true;

        // 새를 kinematic으로 변경하여 멈춤
        SetBirdPhysics(isKinematic: true, resetVelocity: true);

        HideArrow();

        Debug.Log("Bird stopped. Touch to restart.");
    }

    void StopBirdOnFloor()
    {
        if (_birdRigidbody == null) return;

        // 새를 멈춤
        _birdRigidbody.velocity = Vector2.zero;
        _birdRigidbody.angularVelocity = 0f;

        // 현재 위치를 새로운 시작 위치로 설정
        _isDragging = false;
        _hasLaunched = false;
        _isOnFloor = false;
        _isFailed = false; // floor에서 멈춘 경우는 리셋 안 함, 다시 발사 가능
        _startPosition = transform.position;
        _currentDragPosition = _startPosition;

        // 새를 kinematic으로 변경하여 멈춤
        SetBirdPhysics(isKinematic: true, resetVelocity: true);

        HideArrow();

        Debug.Log("Bird stopped on floor. Can launch again from current position.");
    }

    // 게임 성공 or 실패
    void ResetBirdState()
    {
        _startPosition = _initialPosition;
        
        _isDragging = false;
        _hasLaunched = false;
        _isOnFloor = false;
        _isFailed = false;
        _isSuccess = false; // 성공 상태 리셋
        _currentDragPosition = _initialPosition;

        transform.position = _initialPosition;
        transform.rotation = Quaternion.identity;

        SetBirdPhysics(isKinematic: true, resetVelocity: true);
        HideArrow();
        
        // 성공 스프라이트 숨기기
        HideSuccessSprite();

        Debug.Log("Bird reset to initial position..");
    }
    
    void HideSuccessSprite()
    {
        if (_successSpriteCoroutine != null)
        {
            StopCoroutine(_successSpriteCoroutine);
            _successSpriteCoroutine = null;
        }
        
        if (_successSpriteInstance != null)
        {
            Destroy(_successSpriteInstance);
            _successSpriteInstance = null;
        }
    }

    void OnDestroy()
    {
        // Arrow 인스턴스 정리
        if (_dragArrowInstance != null)
        {
            Destroy(_dragArrowInstance);
        }
        
        // 성공 스프라이트 정리
        HideSuccessSprite();
    }
}
