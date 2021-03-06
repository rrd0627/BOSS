﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Boss2_AllFire : MonoBehaviour
{
    private Vector2 dir;
    private float movespeed;
    private float damage_amount;
    private float multiple = 3;
    void Start()
    {
        dir = new Vector2(1, 0);
        movespeed = 15f;
        Destroy(this.gameObject, 5); //5초뒤에 없어짐
        StartCoroutine(Invincible_Cancel());
        damage_amount = BossStatus.instance.PhysicalAttackPower;
    }

    // Update is called once per frame
    void Update()
    {
        this.transform.Translate(dir * Time.deltaTime * movespeed);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            collision.GetComponent<CharacterStat>().GetDamage(damage_amount * multiple, true);
        }
    }
    IEnumerator Invincible_Cancel()
    {
        yield return new WaitForSeconds(2f);
        for(int i=0;i<GameManage.instance.player.Length;i++)
        {
            GameManage.instance.player[i].GetComponent<CharacterStat>().is_Invincible = false;
        }
    }


}
