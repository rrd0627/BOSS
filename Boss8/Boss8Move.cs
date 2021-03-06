﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boss8Move : MonoBehaviour
{
    // 현재 실행할 코루틴을 가지고 있음. 
    IEnumerator _current;


    // 어그로 구조체
    public struct _aggro
    {
        public float distance;
        public float attackAggro;
        public float healAggro;
        public float goldHand;

        public _aggro(float dis, float atAg, float hAg, float goldHand)
        {
            this.distance = dis;
            this.attackAggro = atAg;
            this.healAggro = hAg;
            this.goldHand = goldHand;
        }
    }

    // 플레이어의 수를 확인하고 배열의 인덱스로 사용
    enum _PLAYER
    {
        NONE = -1,
        PLAYER_1 = 0,
        PLAYER_2,
        PLAYER_NUM
    }

    enum _BossAct
    {
        Stay = -1,
        Chasing,
        Attacking, // 물공
        Swift,      // 물공
        Canon,      // 물공
		Soul,       // 물공
        WindWalk,   // 물공
        Steal,      // 혼합
        Scatter,    // 마공
    }

    // 타겟 플레이어 임시 저장 변수
    [HideInInspector]
    public GameObject temp_target_player;

    public GameObject[] skill_prefab; //이거 나중에 resource로 옮겨서 소스에서 넣기

    // 플레이어1 오브젝트
    public GameObject[] player_Object;
    // 이 부분이 player_Object = GameObject.FindobjectsWithtag("Player"); 가 start에 들어있었음... 문제는 enum _Player임. 멀티할때는 총 8개까지 늘어나야 할 텐데... 구조가 어떻게 뒤바뀔지 모르겠다.

    public float[] skill_cooltime; //스킬 쿨타임 용 배열  나중엔 private으로 바꿔야함    
    [HideInInspector]
    public float[] AggroSum;

    // 플레이어 어그로 구조체를 담아서 보관할 배열
    public _aggro[] Players_Aggro;

    // 이동 속도
    [HideInInspector]
    public float walkSpeed = 1.0f;

    // 도착했는가 (도착했다 true / 도착하지 않았다 false)
    [HideInInspector]
    public bool arrived = false;

   
    //private bool is_sandstrom_used = false;

    private bool is_HP_first = false;
    private bool is_HP_second = false;
    private bool is_HP_third = false;

    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private int boss_act_num = 0;

    private int max_aggro_playerNum;

    private float[] skill_cooltime_temp;
    // 시간 임시 변수

    private float max_aggro;

    // 목적지에 도착했다고 보는 정지 거리
    const float StoppingDistance = 2 + .0f;    

    private GameObject target_player;

    private GameObject[] skill_prefab_temp; //스킬 이펙트 넣기 위한 배열
    /*
     * 0.노말 어택
     * 1.기모으기 이펙트
     * 2.일자로 나가는 범위 이펙트
     * 6.기둥 마법진
     * 7.기둥
     * 10.몬스터 나오는곳 표시
     * 11.몬스터
     * 12.모래바람
     */
    private WaitForSeconds waitforsec;

    private Vector3 pos_memo; //pos기억용

    private Vector3 vector3_temp; //스킬 활용할때 쓰는 임시용 vector

    public Vector3 vector3_throw_attack;

    // 현재 이동 속도
    private Vector3 velocity = Vector3.zero;

    // 목적지
    private Vector3 destination;

    public Camera main;

    private Vector3 temp_vec;

    private List<GameObject> normal_attack;

    private bool[] fog;

    private int ult_count = 0;
    private void Start()
    {
        player_Object = GameObject.FindGameObjectsWithTag("Player");
        player_Object = BossStatus.instance.PlayerObjectSort(player_Object);
        Players_Aggro = new _aggro[player_Object.Length];
        AggroSum = new float[player_Object.Length];
        InitializeAggro();
        skill_prefab_temp = new GameObject[skill_prefab.Length];
        skill_cooltime_temp = new float[skill_cooltime.Length];
        walkSpeed = BossStatus.instance.moveSpeed;
        normal_attack = new List<GameObject>();
        for (int i = 0; i < skill_cooltime.Length; i++)
        {
            skill_cooltime_temp[i] = 0;
        }
        _animator = this.GetComponent<Animator>();
        _spriteRenderer = this.GetComponent<SpriteRenderer>();
        waitforsec = new WaitForSeconds(0.01f);
        StartCoroutine(Exec());
        fog = new bool[16];
        for (int i = 0; i < fog.Length; i++)
            fog[i] = false;

        
    }

    private void Update()
    {
        if (NetworkManager.instance.is_multi && NetworkManager.instance.is_host)
        {
            BossAct(boss_act_num);
        }
        else if (NetworkManager.instance.is_multi && !NetworkManager.instance.is_host)
        {
            return;
        }
        else
            BossAct(boss_act_num);

    }

    IEnumerator Exec()
    {
        while (true)
        {
            yield return new WaitForSeconds(2.0f);
            //AggroSum = BossStatus.instance.ReturnAggro();

        }
    }
    public void EndofGame()
    {
        boss_act_num = (int)_BossAct.Stay;
    }

    public void SelectTarget()
    {
        max_aggro = -100;
        for (int i = 0; i < player_Object.Length; i++)
        {
            if (AggroSum[i] > max_aggro)
            {
                max_aggro = AggroSum[i];
                max_aggro_playerNum = i;
            }
        }
        if (target_player != player_Object[max_aggro_playerNum])
        {
            target_player = player_Object[max_aggro_playerNum];
            BossStatus.instance.target_player = target_player;
        }

    }

    private bool MovetoTarget()
    {
        if (GameManage.instance.IsGameEnd)
        {
            boss_act_num = (int)_BossAct.Stay;
            return true;
        }
        Move(target_player.transform.position);
        return false;
    }

    public void Move(Vector3 destination)
    {
        Vector3 Destination = destination;
        Destination.z = transform.position.z;


        // 목적지까지의 거리와 방향을 구한다.
        // 방향
        Vector3 direction = (Destination - transform.position).normalized;
        // 거리
        float distance = Vector3.Distance(this.gameObject.transform.position, Destination);


        _animator.SetFloat("DirX", direction.x);
        _animator.SetFloat("DirY", direction.y);
        // 현재 속도를 보관
        Vector3 currentVelocity = velocity;

        // 목적지에 가까이 왔으면 도착
        if (arrived || distance < StoppingDistance)
        {
            arrived = true;
        }

        // 일정거리에 도달하지 않으면 도착하지 않은것
        if (distance > StoppingDistance)
        {
            _animator.SetBool("Running", true);
            arrived = false;
        }

        // 도착했으면 멈추고 아니면 속도를 구함
        if (arrived)
        {
            velocity = Vector3.zero;
            if (_animator.GetBool("Running"))
            {
                _animator.SetBool("Running", false);
            }
        }
        else
            velocity = direction * walkSpeed;

        // 보간처리
        velocity = Vector3.Lerp(currentVelocity, velocity, Mathf.Min(Time.deltaTime * 5.0f, 1.0f));
        velocity.z = 0;
        walkSpeed = BossStatus.instance.moveSpeed;
        this.gameObject.transform.Translate(velocity * 2.0f * Time.deltaTime);

    }

    // 최초 어그로 초기화.
    public void InitializeAggro()
    {
        for (int i = 0; i < player_Object.Length; i++)
        {
            Players_Aggro[i] = new _aggro(0, 0, 0, 0);
            AggroSum[i] = 0;
        }
    }

    public void DecreaseAggro()
    {
        for (int i = 0; i < (int)_PLAYER.PLAYER_NUM; i++)
        {
            AggroSum[i] -= 2.0f;
        }
    }

    private void BossAct(int act_num)
    {
        BossStatus.instance.MP += Time.deltaTime;
        if (BossStatus.instance.MP > BossStatus.instance.MaxMp)
        {
            BossStatus.instance.MP = BossStatus.instance.MaxMp;
        }
        if (BossStatus.instance.HP <= 0)
        {
            act_num = (int)_BossAct.Stay;
        }


        skill_cooltime_temp[1] += Time.deltaTime; //Steal
        skill_cooltime_temp[2] += Time.deltaTime; //Canon
        skill_cooltime_temp[3] += (UpLoadData.boss_level*0.4f+1)*Time.deltaTime; //WindWalk

        SelectTarget();

        switch (act_num)
        {

            case (int)_BossAct.Stay:
                if (BossStatus.instance.Silent)
                {
                    BossGetSilent();
                    boss_act_num = (int)_BossAct.Chasing;
                    BossStatus.instance.BossSilentAble(false);
                    BossStatus.instance.Silent = false;
                }
                break;
            case (int)_BossAct.Chasing:
                if (MovetoTarget()) return;
                //boss_act_num = (int)_BossAct.Scatter; //테스트용으로 쓰는 코드
                //break;
                if (BossStatus.instance.MP >= BossStatus.instance.MaxMp) //레데 궁 : mp max일때 소환
                {
                    BossStatus.instance.MP = 0;
                    if (ult_count >= 2)
                    {
                        ult_count = 1;
                        BossStatus.instance.GetBuff(8);
                    }
                    boss_act_num = (int)_BossAct.Swift;
                    break;
                }
                if (BossStatus.instance.HP / BossStatus.instance.MaxHP < 0.775 && !is_HP_first) //불도저
                {
                    is_HP_first = true;
                    boss_act_num = (int)_BossAct.Scatter;
                    break;
                }
                if (BossStatus.instance.HP / BossStatus.instance.MaxHP < 0.5 && !is_HP_second)
                {
                    is_HP_second = true;
                    boss_act_num = (int)_BossAct.Scatter;
                    break;
                }
                if (BossStatus.instance.HP / BossStatus.instance.MaxHP < 0.225 && !is_HP_third)
                {
                    is_HP_third = true;
                    boss_act_num = (int)_BossAct.Scatter;
                    break;
                }
                if (skill_cooltime_temp[2] > skill_cooltime[2]) //Canon
                {
                    skill_cooltime_temp[2] = 0;
                    boss_act_num = (int)_BossAct.Canon;
                    break;
                }
                if (arrived) //평타
                {
                    skill_cooltime_temp[0] = 0;
                    boss_act_num = (int)_BossAct.Attacking;
                    break;
                }
                if (skill_cooltime_temp[1] > skill_cooltime[1]) //Steal
                {
                    skill_cooltime_temp[1] = 0;
                    boss_act_num = (int)_BossAct.Steal;
                    break;
                }
                if (skill_cooltime_temp[3] > skill_cooltime[3]) //WindWalk
                {
                    skill_cooltime_temp[3] = 0;
                    boss_act_num = (int)_BossAct.WindWalk;
                    break;
                }
                break;

            case (int)_BossAct.Attacking:
                StartCoroutine(Attack_active());
                boss_act_num = (int)_BossAct.Stay;
                break;
            case (int)_BossAct.Swift:
                StartCoroutine(SwiftSkill());
                boss_act_num = (int)_BossAct.Stay;
                break;
            case (int)_BossAct.Canon:
                StartCoroutine(CanonSkill());
                boss_act_num = (int)_BossAct.Stay;
                break;
            case (int)_BossAct.WindWalk:
                StartCoroutine(WindWalkSkill());
                boss_act_num = (int)_BossAct.Stay;
                break;
            case (int)_BossAct.Steal:
                StartCoroutine(StealSkill());
                boss_act_num = (int)_BossAct.Stay;
                break;
            case (int)_BossAct.Scatter:
                StartCoroutine(ScatterSkill());
                boss_act_num = (int)_BossAct.Stay;
                break;                
        }
    }

    // 기본 공격
    IEnumerator Attack_active()
    {
        _animator.SetBool("Running", false);
        _animator.SetBool("Attack", true);

        int fog_num;
        int width;
        int height;
        GameObject[] Players;
        for(int i=0;i<fog.Length;i++)
        {
            if(!fog[i])
            {
                break;
            }
            if(i==fog.Length-1)
            {
                Players = GameObject.FindGameObjectsWithTag("Player");
                
                for(int j=0;j<Players.Length;j++)
                {
                    Players[j].GetComponent<CharacterStat>().GetDamage(99999, true);
                }
                yield return new WaitForSeconds(2f);

                boss_act_num = (int)_BossAct.Chasing;
                yield return 0;
            }
        }   

        while(true)
        {
            fog_num = Random.Range(0, fog.Length);
            if (fog[fog_num]) continue;
            fog[fog_num] = true;

            width = fog_num / 4; //0,1,2,3
            height = fog_num - width * 4; //0,1,2,3
            break;
        }
        
        Vector2 pos;
        pos = new Vector2((width-1.5f)*6, (height-1.5f)*4.5f);
        SoundManager.instance.Play(61);
        skill_prefab_temp[26] = Instantiate(skill_prefab[26], pos,Quaternion.identity); // Warning
        skill_prefab_temp[26].transform.localScale = new Vector3(0.8f, 0.8f);

        yield return new WaitForSeconds(1f);
        _animator.SetBool("Attack", false);

        _animator.SetBool("AttackEnd", true);
        yield return new WaitForSeconds(0.1f);
        _animator.SetBool("AttackEnd", false);


        boss_act_num = (int)_BossAct.Chasing;
        yield return 0;
    }

    IEnumerator SwiftSkill() // 7s
    {
        _animator.SetBool("Running", false);
        _animator.SetBool("Attack", true);

        StartCoroutine(Vanish());

        yield return new WaitForSeconds(1f);

        skill_prefab_temp[10] = Instantiate(skill_prefab[10], this.transform.position,Quaternion.identity); // Warning
        skill_prefab_temp[10].transform.localScale = Vector3.one * 0.5f;
        
        yield return new WaitForSeconds(5f);

        StartCoroutine(Appear());
        yield return new WaitForSeconds(1f);

        _animator.SetBool("Attack", false);

        _animator.SetBool("AttackEnd", true);
        yield return new WaitForSeconds(0.1f);
        _animator.SetBool("AttackEnd", false);

        boss_act_num = (int)_BossAct.Chasing;
    }
    IEnumerator CanonSkill()//4.6s
    {
        _animator.SetBool("Running", false);
        _animator.SetBool("Attack", true);

        Vector3 TargetPos;
        TargetPos = target_player.transform.position;

        Vector3 vec2;
        vec2 = TargetPos;
        yield return new WaitForSeconds(1f);

        skill_prefab_temp[22] = Instantiate(skill_prefab[22], TargetPos, Quaternion.identity); // Warning
        skill_prefab_temp[22].transform.localScale = Vector3.one * 0.5f;
        Destroy(skill_prefab_temp[22], 2.5f);
        yield return new WaitForSeconds(2f);
        for(float i=0;i<8;i+=1)
        {
            vec2.x = TargetPos.x + Mathf.Cos(Mathf.Deg2Rad*i * 360f / 8f)*2f * BossStatus.instance.transform.localScale.x;
            vec2.y = TargetPos.y + Mathf.Sin(Mathf.Deg2Rad*i * 360f / 8f)*2f * BossStatus.instance.transform.localScale.x;
            skill_prefab_temp[22] = Instantiate(skill_prefab[22], vec2, Quaternion.identity); // Warning
            skill_prefab_temp[22].transform.localScale = Vector3.one;
            Destroy(skill_prefab_temp[22], 0.5f);
        }
        skill_prefab_temp[1] = Instantiate(skill_prefab[1], TargetPos, Quaternion.identity); // Warning
        skill_prefab_temp[1].transform.localScale = Vector3.one* 4 * BossStatus.instance.transform.localScale.x * (UpLoadData.boss_level*0.15f+1);
        Destroy(skill_prefab_temp[1], 0.5f);
        yield return new WaitForSeconds(0.5f);

        main.GetComponent<Camera_move>().VivrateForTime(0.5f, 0.2f);

        SoundManager.instance.Play(64);
        skill_prefab_temp[23] = Instantiate(skill_prefab[23], TargetPos, Quaternion.identity); // Warning
        skill_prefab_temp[23].transform.localScale = Vector3.one *BossStatus.instance.transform.localScale.x*0.5f * (UpLoadData.boss_level * 0.15f + 1);

        yield return new WaitForSeconds(1f);

        _animator.SetBool("Attack", false);

        _animator.SetBool("AttackEnd", true);
        yield return new WaitForSeconds(0.1f);
        _animator.SetBool("AttackEnd", false);

        boss_act_num = (int)_BossAct.Chasing;
    }
    IEnumerator WindWalkSkill()
    {   
        WaitForSeconds waittime;
        waittime = new WaitForSeconds(0.01f);
        float movespeed = 10f;
        //일정시간마다 분신처럼 소환되고
        //빠르게 target 뒤로 (보스 나)  -> (나 보스) 로 
        Vector2 dir;
        dir = Random.insideUnitCircle;
        Vector3 targetpos;
        GameObject[] Targets;
        GameObject Target;
        float timer = 0;
        Targets = GameObject.FindGameObjectsWithTag("Player");
        float Target_dis = 0f;
        int Target_index=0;
        for(int i=0;i<Targets.Length;i++)
        {
            if(Vector2.Distance(Targets[i].transform.position,this.transform.position)>Target_dis)
            {
                Target_dis = Vector2.Distance(Targets[i].transform.position, this.transform.position);
                Target_index = i;
            }
        }
        Target = Targets[Target_index];

        targetpos = Target.transform.position - BossStatus.instance.transform.position;
        targetpos.z = 0;
        targetpos.Normalize();
        targetpos *= 2f;
        targetpos += Target.transform.position;

        skill_prefab_temp[1] = Instantiate(skill_prefab[1], targetpos, Quaternion.identity); // Warning
        skill_prefab_temp[1].transform.localScale = Vector3.one;
        yield return new WaitForSeconds(0.5f);

        StartCoroutine(MakeDummy(targetpos));
        //이동
        while (true)
        {
            if (Vector2.Distance(targetpos, this.transform.position) < 1f) break;
            dir += ((Vector2)(targetpos - BossStatus.instance.transform.position)).normalized*0.2f;
            dir.Normalize();
            this.transform.Translate(dir * Time.deltaTime * movespeed);
            yield return waittime;
        }
        Destroy(skill_prefab_temp[1]);


        _animator.SetFloat("DirX", Target.transform.position.x - BossStatus.instance.transform.position.x);
        _animator.SetFloat("DirY", Target.transform.position.y - BossStatus.instance.transform.position.y);


        _animator.SetBool("Attack", true);
        yield return new WaitForSeconds(0.1f);
        
        _animator.SetBool("Attack", false);
        yield return new WaitForSeconds(0.4f);

        if (Target.transform.position.x>this.transform.position.x)
            skill_prefab_temp[25] = Instantiate(skill_prefab[25], this.transform.position, Quaternion.Euler(0,0, Mathf.Rad2Deg * Mathf.Atan((-Target.transform.position.y + this.transform.position.y) / (-Target.transform.position.x + this.transform.position.x))));
        else
            skill_prefab_temp[25] = Instantiate(skill_prefab[25], this.transform.position, Quaternion.Euler(0, 0, 180+Mathf.Rad2Deg * Mathf.Atan((-Target.transform.position.y + this.transform.position.y) / (-Target.transform.position.x + this.transform.position.x))));
        skill_prefab_temp[25].transform.localScale = Vector3.one *BossStatus.instance.transform.localScale.x;

        yield return new WaitForSeconds(1f);
        Collider2D[] Players;
        bool is_CharacterIn = false;
        Color _color;
        int count=0;
        //중력장
        skill_prefab_temp[27] = Instantiate(skill_prefab[27], this.transform);

        BossStatus.instance.BossSilentAble(true, skill_prefab_temp[27]);
		SoundManager.instance.Play(59);
        SoundManager.instance.SetLoop(59);
        while (true)
        {
            Players = Physics2D.OverlapCircleAll(this.transform.position, 4f*BossStatus.instance.transform.localScale.x);
            is_CharacterIn = false;
            count++;
            timer += Time.deltaTime;
            for (int i=0;i<Players.Length;i++)
            {
                if (Players[i].CompareTag("Player"))
                {
                    is_CharacterIn = true;
                    
                    this.transform.localScale += Vector3.one * 0.001f;
                    if (count>10)
                    {
                        BossStatus.instance.PhysicalAttackPower += BossStatus.instance.PhysicalAttackPower * 0.01f;
                        BossStatus.instance.MagicAttackPower += BossStatus.instance.MagicAttackPower * 0.01f;

                        _color = Players[i].GetComponent<SpriteRenderer>().color;
                        _color.a -= 0.01f;
                        Players[i].GetComponent<SpriteRenderer>().color = _color;
                        if (_color.a < 0.2f)
                        {
                            Players[i].GetComponent<CharacterStat>().GetDamage(99999, true);
                        }
                        skill_prefab_temp[28] = Instantiate(skill_prefab[28], Players[i].transform.position, Quaternion.identity);
                        skill_prefab_temp[28].GetComponent<SpriteRenderer>().sprite = Players[i].GetComponent<SpriteRenderer>().sprite;
                        skill_prefab_temp[28].transform.localScale = Vector3.one * 2f;
                    }
                }
            }
            if (timer > 10)               
                break;
            if (count > 10)
                count = 0;
            if (!is_CharacterIn)            
                break;
            yield return waittime;
        }
        SoundManager.instance.Stop(59);
        SoundManager.instance.SetLoopCancel(59);

        Destroy(skill_prefab_temp[27]);

        BossStatus.instance.BossSilentAble(false);

        boss_act_num = (int)_BossAct.Chasing;
    }
    IEnumerator MakeDummy(Vector2 targetpos)
    {
        WaitForSeconds waittime;
        waittime = new WaitForSeconds(0.1f);
        Color alpha;
        alpha = new Color(1, 1, 1, 0.5f);
        while(true)
        {
            if (Vector2.Distance(targetpos, this.transform.position) < 1f) break;
            skill_prefab_temp[24] = Instantiate(skill_prefab[24], this.transform.position, Quaternion.identity); // Warning
            skill_prefab_temp[24].GetComponent<SpriteRenderer>().sprite = this.gameObject.GetComponent<SpriteRenderer>().sprite;
            skill_prefab_temp[24].GetComponent<SpriteRenderer>().color = alpha;
            skill_prefab_temp[24].transform.localScale = Vector3.one * this.transform.localScale.x;
            StartCoroutine(VanishSprite(skill_prefab_temp[24]));
            yield return waittime;
        }
    }
    IEnumerator VanishSprite(GameObject _object)
    {
        WaitForSeconds waittime;
        waittime = new WaitForSeconds(0.01f);
        Color newColor;
        newColor = new Color(1, 1, 1, 0.5f);

        float alpha = 0.5f;
        while (alpha > 0)
        {
            _object.GetComponent<SpriteRenderer>().color = newColor;
            alpha -= 0.03f;
            newColor.a = alpha;
            yield return waittime;
        }
        Destroy(_object);
    }

    IEnumerator Vanish()
    {
        WaitForSeconds waittime;
        waittime = new WaitForSeconds(0.01f);
        Color newColor;
        newColor = new Color(1, 1, 1, 1);

        float alpha = 1;
        while(alpha>0)
        {
            BossStatus.instance.GetComponent<SpriteRenderer>().color = newColor;
            alpha -= 0.01f;
            newColor.a = alpha;
            yield return waittime;
        }
        BossStatus.instance.GetComponent<Collider2D>().enabled = false;
        BossStatus.instance.tag = "Ghost";
    }
    IEnumerator Appear()
    {
        WaitForSeconds waittime;
        waittime = new WaitForSeconds(0.01f);
        Color newColor;
        newColor = new Color(1, 1, 1, 0);

        float alpha = 0;
        while (alpha < 1)
        {
            BossStatus.instance.GetComponent<SpriteRenderer>().color = newColor;
            alpha += 0.01f;
            newColor.a = alpha;
            yield return waittime;
        }
        BossStatus.instance.GetComponent<Collider2D>().enabled = true;
        BossStatus.instance.tag = "Boss";

    }

	IEnumerator StealSkill()
    {
        int num = target_player.GetComponent<CharacterStat>().char_num;

        GameObject temp = target_player;
        skill_prefab_temp[1] = Instantiate(skill_prefab[1], temp.transform);
        Destroy(skill_prefab_temp[1], 2f);
        yield return new WaitForSeconds(2f);
        if(num == 0 || num == 5)
        {
            SoundManager.instance.Play(62);
        }
        else
        {
            SoundManager.instance.Play(63);

        }
        switch (num)
        {
            case 0:
                skill_prefab_temp[2] = Instantiate(skill_prefab[2], temp.transform.position, Quaternion.identity);
                yield return new WaitForSeconds(3f);
                skill_prefab_temp[12] = Instantiate(skill_prefab[12], this.transform);
                Destroy(skill_prefab_temp[12], 2f);
                if (temp.transform.position.x - this.transform.position.x > 0)
                {
                    skill_prefab_temp[12].transform.rotation = Quaternion.Euler(0, 0, Mathf.Rad2Deg * Mathf.Atan((temp.transform.position.y - this.transform.position.y) / (temp.transform.position.x - this.transform.position.x)));
                }
                else
                {
                    skill_prefab_temp[12].transform.rotation = Quaternion.Euler(0, 0, 180 + Mathf.Rad2Deg * Mathf.Atan((temp.transform.position.y - this.transform.position.y) / (temp.transform.position.x - this.transform.position.x)));

                }
                Quaternion temp_rotation;
                temp_rotation = skill_prefab_temp[12].transform.rotation;
                yield return new WaitForSeconds(2f);
                _animator.SetBool("Attack", true);
                yield return new WaitForSeconds(0.1f);
                _animator.SetBool("Attack", false);

                skill_prefab_temp[13] = Instantiate(skill_prefab[13], this.transform);
                Destroy(skill_prefab_temp[13], 5f);
                skill_prefab_temp[13].transform.rotation = temp_rotation;
                yield return new WaitForSeconds(3f);



                boss_act_num = (int)_BossAct.Chasing;
                yield return 0;

                break;

            case 1:
                skill_prefab_temp[3] = Instantiate(skill_prefab[3], temp.transform.position, Quaternion.identity);
                yield return new WaitForSeconds(3f);
                skill_prefab_temp[0] = Instantiate(skill_prefab[0], this.transform);
                skill_prefab_temp[0].transform.localScale = new Vector3(30, 4, 3);
                Destroy(skill_prefab_temp[0], 2f);


                if (temp.transform.position.x - this.transform.position.x > 0)
                {
                    skill_prefab_temp[0].transform.rotation = Quaternion.Euler(0, 0, Mathf.Rad2Deg * Mathf.Atan((temp.transform.position.y - this.transform.position.y) / (temp.transform.position.x - this.transform.position.x)));
                }
                else
                {
                    skill_prefab_temp[0].transform.rotation = Quaternion.Euler(0, 0, 180 + Mathf.Rad2Deg * Mathf.Atan((temp.transform.position.y - this.transform.position.y) / (temp.transform.position.x - this.transform.position.x)));

                }
                Quaternion rotation = skill_prefab_temp[0].transform.rotation;

                yield return new WaitForSeconds(2f);
                main.GetComponent<Camera_move>().VivrateForTime(1f, 0.1f);
                _animator.SetBool("Attack", true);
                yield return new WaitForSeconds(0.1f);
                _animator.SetBool("Attack", false);

                Vector3 boss_pos = this.transform.position;
                skill_prefab_temp[14] = Instantiate(skill_prefab[14], this.transform.position, Quaternion.identity);
                skill_prefab_temp[14].transform.rotation = rotation;
                Destroy(skill_prefab_temp[14], 5f);
                yield return new WaitForSeconds(4f);

                boss_act_num = (int)_BossAct.Chasing;
                yield return 0;

                break;

            case 2:
                skill_prefab_temp[4] = Instantiate(skill_prefab[4], temp.transform.position, Quaternion.identity);
                yield return new WaitForSeconds(3f);

                Vector2[] temp_position_array = new Vector2[10];

                for(int i= 0; i <10; i++)
                {
                    temp_position_array[i] = (Vector2)this.transform.position + Random.insideUnitCircle * 10f;
                    skill_prefab_temp[1] = Instantiate(skill_prefab[1], temp_position_array[i], Quaternion.identity);
                    skill_prefab_temp[1].transform.localScale = Vector3.one * 6f;
                    Destroy(skill_prefab_temp[1], 2f);
                }

                yield return new WaitForSeconds(2f);

                _animator.SetBool("Attack", true);
                yield return new WaitForSeconds(0.1f);
                _animator.SetBool("Attack", false);

                for (int i = 0; i < 10; i++)
                {
                    skill_prefab_temp[15] = Instantiate(skill_prefab[15], temp_position_array[i], Quaternion.identity);
                    main.GetComponent<Camera_move>().VivrateForTime(0.2f, 0.1f);
                    Destroy(skill_prefab_temp[15], 0.5f);
                    yield return new WaitForSeconds(0.2f);
                }
                yield return new WaitForSeconds(1f);
                
                boss_act_num = (int)_BossAct.Chasing;
                yield return 0;
                break;

            case 3:
                skill_prefab_temp[5] = Instantiate(skill_prefab[5], temp.transform.position, Quaternion.identity);
                yield return new WaitForSeconds(3f);
                skill_prefab_temp[1] = Instantiate(skill_prefab[1], temp.transform);
                Destroy(skill_prefab_temp[1], 2f);
                yield return new WaitForSeconds(2f);
                Vector2[] warning_position = new Vector2[4];
                warning_position[0] = (Vector2)this.transform.position;

                for(int i = 0; i < warning_position.Length - 1; i++)
                {
                    warning_position[i + 1] =  warning_position[i] + (Vector2)(temp.transform.position - this.transform.position) / 3f;
                }

                for(int i = 1; i < warning_position.Length; i++)
                {
                    skill_prefab_temp[1] = Instantiate(skill_prefab[1], warning_position[i], Quaternion.identity);
                    skill_prefab_temp[1].transform.localScale = Vector3.one * (i+1) * 3;
                    Destroy(skill_prefab_temp[1], 1f);
                    yield return new WaitForSeconds(0.5f);

                }
                _animator.SetBool("Attack", true);
                yield return new WaitForSeconds(0.1f);
                _animator.SetBool("Attack", false);

                for (int i = 1; i < warning_position.Length; i++)
                {
                    main.GetComponent<Camera_move>().VivrateForTime(0.3f, i * 0.05f);
                    skill_prefab_temp[16] = Instantiate(skill_prefab[16], warning_position[i], Quaternion.identity);
                    skill_prefab_temp[16].transform.localScale = Vector3.one * (i+1) * 2;
                    skill_prefab_temp[16].GetComponent<Move_Hammer>().GetMult(i+1);
                    if (temp.transform.position.x - this.transform.position.x > 0)
                    {
                        skill_prefab_temp[16].transform.rotation = Quaternion.Euler(0, 0, Mathf.Rad2Deg * Mathf.Atan((temp.transform.position.y - this.transform.position.y) / (temp.transform.position.x - this.transform.position.x)));
                    }
                    else
                    {
                        skill_prefab_temp[16].transform.rotation = Quaternion.Euler(0, 0, 180 + Mathf.Rad2Deg * Mathf.Atan((temp.transform.position.y - this.transform.position.y) / (temp.transform.position.x - this.transform.position.x)));
                    }
                    Destroy(skill_prefab_temp[16], 0.5f);
                    yield return new WaitForSeconds(0.5f);
                }
                yield return new WaitForSeconds(1f);


                boss_act_num = (int)_BossAct.Chasing;
                yield return 0;
                break;

            case 4:
                skill_prefab_temp[6] = Instantiate(skill_prefab[6], temp.transform.position, Quaternion.identity);
                yield return new WaitForSeconds(3f);
                skill_prefab_temp[1] = Instantiate(skill_prefab[1], this.transform);
                skill_prefab_temp[1].transform.localScale = Vector3.one * 15f;
                Destroy(skill_prefab_temp[1], 2f);
                yield return new WaitForSeconds(2f);
                
                main.GetComponent<Camera_move>().VivrateForTime(1f);
                _animator.SetBool("Attack", true);
                yield return new WaitForSeconds(0.1f);
                _animator.SetBool("Attack", false);
                skill_prefab_temp[17] = Instantiate(skill_prefab[17], this.transform);
                skill_prefab_temp[17].transform.localScale = Vector3.one * 2f;

                Destroy(skill_prefab_temp[17], 1f);
                yield return new WaitForSeconds(2f);
                
                boss_act_num = (int)_BossAct.Chasing;
                yield return 0;
                break;

            case 5:
                skill_prefab_temp[7] = Instantiate(skill_prefab[7], temp.transform.position, Quaternion.identity);
                yield return new WaitForSeconds(3f);
                Vector3 position_temp = temp.transform.position;
                skill_prefab_temp[1] = Instantiate(skill_prefab[1], temp.transform.position, Quaternion.identity);
                skill_prefab_temp[1].transform.localScale = Vector3.one * 10f;
                Destroy(skill_prefab_temp[1], 1f);
                yield return new WaitForSeconds(1f);
                _animator.SetBool("Attack", true);
                yield return new WaitForSeconds(0.1f);
                _animator.SetBool("Attack", false);
                position_temp.y += 8f;
                skill_prefab_temp[18] = Instantiate(skill_prefab[18], position_temp, Quaternion.identity);
                yield return new WaitForSeconds(7f);

                boss_act_num = (int)_BossAct.Chasing;
                yield return 0;
                break;

            case 6:
                skill_prefab_temp[8] = Instantiate(skill_prefab[8], temp.transform.position, Quaternion.identity);
                yield return new WaitForSeconds(4f);
                _animator.SetBool("Attack", true);
                yield return new WaitForSeconds(0.1f);
                _animator.SetBool("Attack", false);

                skill_prefab_temp[19] = Instantiate(skill_prefab[19], this.transform);
                yield return new WaitForSeconds(2f);
                
                boss_act_num = (int)_BossAct.Chasing;
                yield return 0;
                break;

            case 7:
                skill_prefab_temp[9] = Instantiate(skill_prefab[9], temp.transform.position, Quaternion.identity);
                yield return new WaitForSeconds(3f);
                _animator.SetBool("Attack", true);
                yield return new WaitForSeconds(0.1f);
                _animator.SetBool("Attack", false);
                Vector3 this_transform = this.transform.position;
                this_transform.y += 3f;
                this_transform.x += 4f;
                for(int i = 0; i < 6; i++)
                {
                    skill_prefab_temp[20] = Instantiate(skill_prefab[20], this_transform, Quaternion.identity);
                    this_transform.x -= 2f;
                    yield return new WaitForSeconds(1f);
                }

                yield return new WaitForSeconds(1f);
                boss_act_num = (int)_BossAct.Chasing;
                yield return 0;
                break;
        }
    }


    IEnumerator ScatterSkill()
    {
        int _out = Random.Range(0, 3);
        for(int i= 0;i < 3; i++)
        {
            if (_out == i)
                continue;

            skill_prefab_temp[0] = Instantiate(skill_prefab[0], new Vector2(-12, 6 - (i * 6)), Quaternion.identity);
            skill_prefab_temp[0].transform.localScale = new Vector3(25, 6, 1f);
            Destroy(skill_prefab_temp[0], 1f);

            skill_prefab_temp[0] = Instantiate(skill_prefab[0], new Vector2(12, 6 - (i * 6)), Quaternion.identity);
            skill_prefab_temp[0].transform.rotation = Quaternion.Euler(0,0,-180);
            skill_prefab_temp[0].transform.localScale = new Vector3(25, 6, 1f);
            Destroy(skill_prefab_temp[0], 0.9f);
            yield return new WaitForSeconds(1f);
        }
        yield return new WaitForSeconds(0.1f);

        for (int i = 0; i < 3; i++)
        {
            if (_out == i)
                continue;
            _animator.SetBool("Attack", true);
            SoundManager.instance.Play(65);
            skill_prefab_temp[21] = Instantiate(skill_prefab[21], new Vector2(-10, 6 - (i * 6)), Quaternion.identity);
            Destroy(skill_prefab_temp[21], 6.5f);
            skill_prefab_temp[21].GetComponent<Boss8_CircleMissile>().GetDir(Vector2.right);
            skill_prefab_temp[21] = Instantiate(skill_prefab[21], new Vector2(10, 6 - (i * 6)), Quaternion.identity);
            Destroy(skill_prefab_temp[21], 6.5f);
            skill_prefab_temp[21].GetComponent<Boss8_CircleMissile>().GetDir(Vector2.left);
            _animator.SetBool("Attack", false);
            yield return new WaitForSeconds(1f);
        }

        _animator.SetBool("AttackEnd", true);
        yield return new WaitForSeconds(3f);
        _animator.SetBool("AttackEnd", false);

        boss_act_num = (int)_BossAct.Chasing;
        yield return 0;
    }

    public void BossGetSilent()
    {
        StopAllCoroutines();
        StartCoroutine(Exec());
    }
    public void BossGetSilent(GameObject prefab1, GameObject prefab2)
    {
        Destroy(prefab1);
        Destroy(prefab2);
        StopAllCoroutines();
        StartCoroutine(Exec());
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("DeadPlayer"))
        {

            Physics2D.IgnoreCollision(this.GetComponent<CapsuleCollider2D>(), collision.GetComponent<CapsuleCollider2D>());
        }

    }

    public void ChangeBossAct(int index)
    {
        boss_act_num = index;
    }

    public void StopMagneticCor()
    {
        _animator.SetBool("AttackEnd", false);
        _animator.SetBool("MagicEnd", false);


        StopAllCoroutines();
    }
}
