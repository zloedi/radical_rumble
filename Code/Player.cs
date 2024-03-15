using System;
using System.Collections.Generic;

class Player {
    enum State {
        None,
        Ready,
    }

    // player 0 is invalid though, bump it up if you need 4 players
    public const int MAX_PLAYER = 4;

    // tx-ed
    public ushort [] zport = null;
    public byte [] state = null;
    public byte [] team = null;
    public int [] manaFull_ms = null;

    List<Array> _allRows;

    public Player() {
        ArrayUtil.CreateAll( this, MAX_PLAYER, out _allRows );
    }

    public void Reset() {
        ArrayUtil.Clear( _allRows );
    }

    public void Clear( int pl ) {
        ArrayUtil.ClearColumn( _allRows, pl );
    }

    public int Create( int zport, int clock ) {
        if ( ! ArrayUtil.FindFreeColumn( this.zport, out int pl ) ) {
            return 0;
        }
        Clear( pl );
        int team = TeamNeedsPlayers( 0 ) ? 0 : 1;
        this.zport[pl] = ( ushort )zport;
        this.team[pl] = ( byte )team;
        ResetMana( pl, clock );
        return pl;
    }

    public void DestroyByZport( int zp ) {
        Destroy( GetByZPort( zp ) );
    }

    public void Destroy( int pl ) {
        Clear( pl );
    }

    public int TeamByZPort( int zp ) {
        return team[GetByZPort( zp )];
    }
    
    public int GetByZPort( int zp ) {
        for ( int pl = 1; pl < MAX_PLAYER; pl++ ) {
            if ( zport[pl] == zp ) {
                return pl;
            }
        }
        return 0;
    }

    public bool AnyTeamNeedsPlayers() {
        return TeamNeedsPlayers( 0 ) || TeamNeedsPlayers( 1 );
    }

    public bool TeamNeedsPlayers( int team ) {
        GetTeamsPlayerNum( out int nTeam0, out int nTeam1 );
        // fix the check to have more players per team someday
        return ( team == 0 && nTeam0 < 1 ) || ( team == 1 && nTeam1 < 1 );
    }

    public void GetTeamsPlayerNum( out int nTeam0, out int nTeam1 ) {
        nTeam0 = nTeam1 = 0;
        for ( int pl = 1; pl < MAX_PLAYER; pl++ ) {
            if ( zport[pl] == 0 ) {
                continue;
            }
            if ( team[pl] == 0 ) {
                nTeam0++;
            } else {
                nTeam1++;
            }
        }
    }

    public bool IsPlayer( int zport ) {
        foreach ( var zp in this.zport ) {
            if ( zp == zport ) {
                return true;
            }
        }
        return false;
    }

    public bool IsObserver( int zport ) {
        return ! IsPlayer( zport );
    }

    // clash
    const float MANA_GAIN_SPEED = 1 / 2.8f;
    const int MANA_SECOND = ( int )( 1000 / MANA_GAIN_SPEED );
    const int MANA_MAX = 10 * MANA_SECOND;

    public void ResetMana( int pl, int clock ) {
        manaFull_ms[pl] = clock + MANA_MAX;
    }

    public float Mana( int pl, int clock ) {
        int empty = Math.Max( 0, manaFull_ms[pl] - clock );
        return ( MANA_MAX - empty ) / ( float )MANA_SECOND;
    }

    public bool EnoughMana( int pl, int amount, int clock ) {
        amount *= MANA_SECOND;
        int empty = amount + Math.Max( 0, manaFull_ms[pl] - clock );
        return empty < MANA_MAX;
    }

    public bool ConsumeMana( int pl, int amount, int clock ) {
        if ( EnoughMana( pl, amount, clock ) ) {
            if ( manaFull_ms[pl] < clock ) {
                manaFull_ms[pl] = clock;
            }
            manaFull_ms[pl] += amount * MANA_SECOND;
            return true;
        }
        return false;
    }
}
