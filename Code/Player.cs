using System;
using System.Collections.Generic;

class Player {
    enum State {
        None,
        Ready,
    }

    // player 0 is invalid though, bump it up if you need 4 players
    const int MAX_PLAYER = 4;

    public ushort [] zport = null;
    public byte [] state = null;
    public byte [] team = null;

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

    public int Create( int zport ) {
        if ( ! ArrayUtil.FindFreeColumn( this.zport, out int pl ) ) {
            return 0;
        }
        Clear( pl );
        int team = TeamNeedsPlayers( 0 ) ? 0 : 1;
        this.zport[pl] = ( ushort )zport;
        this.team[pl] = ( byte )team;
        return pl;
    }

    public void Destroy( int pl ) {
        Clear( pl );
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

    // otherwise this is an observer client
    public bool IsPlayer( int zport ) {
        foreach ( var zp in this.zport ) {
            if ( zp == zport ) {
                return true;
            }
        }
        return false;
    }
}
